using ComputerUse.Domain;

namespace ComputerUse.Agent;

public sealed class DiscoveryAgent
{
    private readonly ILanguageModel _model;

    public DiscoveryAgent(ILanguageModel model) =>
        _model = model ?? throw new ArgumentNullException(nameof(model));

    public Task<CapabilityArtifact> DiscoverAsync(
        string goal,
        string baseUrl,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        int maxSteps = Constants.Timing.DefaultMaxDiscoverySteps) =>
        DiscoverAsync(DiscoveryContext.From(goal, baseUrl), surface, allowlist, evidenceDir, maxSteps);

    /// <summary>
    /// LLM-driven discovery: observe → decide → act → record successful actions → emit a draft artifact.
    /// Does not return <see cref="ScriptedLookup"/>; that factory is a deterministic fixture only.
    /// </summary>
    public async Task<CapabilityArtifact> DiscoverAsync(
        DiscoveryContext context,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        int maxSteps = Constants.Timing.DefaultMaxDiscoverySteps)
    {
        Directory.CreateDirectory(evidenceDir);
        var log = new DiscoveryEvidenceLog(evidenceDir);
        var recorder = new DiscoveryRecorder(context);
        log.Write(0, Constants.DiscoveryEvent.RunStarted, new Dictionary<string, string?>
        {
            ["goal"] = context.Goal,
            ["baseUrl"] = context.BaseUrl
        });

        var home = context.BaseUrl + "/";
        var navDenied = PolicyEngine.CheckAction(allowlist, Constants.Action.Navigate, new Uri(home));
        if (navDenied is not null)
            throw new InvalidOperationException(navDenied.Message);
        await surface.NavigateAsync(home);
        recorder.RecordNavigate();
        log.Write(0, Constants.DiscoveryEvent.ActionSucceeded, new Dictionary<string, string?>
        {
            ["tool"] = Constants.Action.Navigate
        });

        for (var i = 0; i < maxSteps; i++)
        {
            var obs = Redaction.Redact(await surface.ObserveAsync());
            var obsFile = $"obs-{i:00}.txt";
            await File.WriteAllTextAsync(Path.Combine(evidenceDir, obsFile), obs);
            log.Write(i, Constants.DiscoveryEvent.Observation, new Dictionary<string, string?> { ["file"] = obsFile });

            var text = await _model.CompleteAsync(BuildPrompt(context.Goal, obs));
            var action = ModelActionParser.TryParse(text);
            if (action is null)
            {
                log.Write(i, Constants.DiscoveryEvent.ActionFailed, new Dictionary<string, string?>
                {
                    ["reason"] = "unparseable model response"
                });
                continue;
            }

            log.Write(i, Constants.DiscoveryEvent.ModelDecision, new Dictionary<string, string?>
            {
                ["tool"] = action.Tool,
                ["parameter"] = action.Parameter,
                ["extractName"] = action.ExtractName
            });

            if (action.Tool == Constants.Action.Finish)
            {
                var artifact = recorder.Emit();
                log.Write(i, Constants.DiscoveryEvent.ArtifactEmitted, new Dictionary<string, string?>
                {
                    ["id"] = artifact.Id,
                    ["steps"] = artifact.Steps.Count.ToString(),
                    ["approvalState"] = artifact.ApprovalState
                });
                log.Write(i, Constants.DiscoveryEvent.RunCompleted, new Dictionary<string, string?>
                {
                    ["status"] = "success"
                });
                return artifact;
            }

            try
            {
                await ExecuteAndRecordAsync(action, i, context, surface, allowlist, recorder, log);
            }
            catch (Exception ex)
            {
                log.Write(i, Constants.DiscoveryEvent.ActionFailed, new Dictionary<string, string?>
                {
                    ["tool"] = action.Tool,
                    ["reason"] = Redaction.Redact(ex.Message)
                });
            }
        }

        throw new InvalidOperationException("Discovery did not finish with a valid recorded artifact.");
    }

    private static async Task ExecuteAndRecordAsync(
        ModelAction action,
        int step,
        DiscoveryContext context,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        DiscoveryRecorder recorder,
        DiscoveryEvidenceLog log)
    {
        var loc = ModelActionParser.Locators(action);
        var strategies = string.Join(",", loc.Select(l => l.Strategy));
        log.Write(step, Constants.DiscoveryEvent.ActionStarted, new Dictionary<string, string?>
        {
            ["tool"] = action.Tool,
            ["locatorStrategies"] = strategies
        });

        Uri? uri = null;
        try { uri = new Uri(await surface.UrlAsync()); } catch { /* ignore */ }
        if (action.Tool == Constants.Action.Navigate)
        {
            var target = ResolveNavigateUrl(action, context);
            uri = new Uri(target);
        }

        var policyAction = action.Tool;
        var denied = PolicyEngine.CheckAction(allowlist, policyAction, uri);
        if (denied is not null)
            throw new InvalidOperationException(denied.Message);

        switch (action.Tool)
        {
            case Constants.Action.Navigate:
                var url = ResolveNavigateUrl(action, context);
                await surface.NavigateAsync(url);
                recorder.Record(new ArtifactStep
                {
                    Action = Constants.Action.Navigate,
                    Url = url.StartsWith(context.BaseUrl, StringComparison.OrdinalIgnoreCase)
                        ? Constants.Template.BaseUrlRoot
                        : url,
                    Risk = Constants.Risk.ReadOnly
                });
                break;
            case Constants.Action.Click:
                if (loc.Count == 0)
                    throw new InvalidOperationException("click requires a locator.");
                await surface.ClickAsync(loc);
                recorder.Record(new ArtifactStep
                {
                    Action = Constants.Action.Click,
                    Locators = loc,
                    Risk = DiscoveryRecorder.RiskFor(Constants.Action.Click)
                });
                break;
            case Constants.Action.Type:
                if (loc.Count == 0)
                    throw new InvalidOperationException("type requires a locator.");
                var concrete = recorder.ConcreteTypeValue(action);
                var persisted = recorder.PersistTypeValue(action);
                await surface.TypeAsync(loc, concrete);
                recorder.Record(new ArtifactStep
                {
                    Action = Constants.Action.Type,
                    Value = persisted,
                    Locators = loc,
                    Risk = DiscoveryRecorder.RiskFor(Constants.Action.Type)
                });
                break;
            case Constants.Action.Extract:
                if (loc.Count == 0)
                    throw new InvalidOperationException("extract requires a locator.");
                var name = string.IsNullOrWhiteSpace(action.ExtractName)
                    ? Constants.Field.Balance
                    : action.ExtractName.Trim();
                var type = NormalizeOutputType(action.OutputType);
                await surface.ExtractAsync(loc);
                recorder.RecordOutput(name, type);
                recorder.Record(new ArtifactStep
                {
                    Action = Constants.Action.Extract,
                    ExtractName = name,
                    Locators = loc,
                    Risk = Constants.Risk.ReadOnly
                });
                log.Write(step, Constants.DiscoveryEvent.Extract, new Dictionary<string, string?>
                {
                    ["extractName"] = name,
                    ["outputType"] = type
                });
                break;
            case Constants.Action.Checkpoint:
                var needle = action.TextContains ?? action.Text;
                if (string.IsNullOrWhiteSpace(needle))
                    throw new InvalidOperationException("checkpoint requires textContains.");
                if (!await surface.PageContainsAsync(needle))
                    throw new InvalidOperationException("checkpoint text was not observed.");
                recorder.Record(new ArtifactStep
                {
                    Action = Constants.Action.Checkpoint,
                    TextContains = needle,
                    Risk = Constants.Risk.ReadOnly
                });
                log.Write(step, Constants.DiscoveryEvent.Checkpoint, new Dictionary<string, string?>
                {
                    ["textContains"] = needle
                });
                break;
            default:
                throw new InvalidOperationException($"Unsupported discovery tool '{action.Tool}'.");
        }

        log.Write(step, Constants.DiscoveryEvent.ActionSucceeded, new Dictionary<string, string?>
        {
            ["tool"] = action.Tool,
            ["locatorStrategies"] = strategies,
            ["status"] = "success"
        });
    }

    private static string ResolveNavigateUrl(ModelAction action, DiscoveryContext context)
    {
        if (string.IsNullOrWhiteSpace(action.Url) || action.Url == "/" ||
            action.Url.Equals(context.BaseUrl, StringComparison.OrdinalIgnoreCase) ||
            action.Url.Equals(context.BaseUrl + "/", StringComparison.OrdinalIgnoreCase))
            return context.BaseUrl + "/";
        if (!Uri.TryCreate(action.Url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("navigate requires an absolute url.");
        return uri.ToString();
    }

    private static string NormalizeOutputType(string? type)
    {
        if (string.Equals(type, Constants.Field.DecimalType, StringComparison.OrdinalIgnoreCase))
            return Constants.Field.DecimalType;
        return Constants.Field.StringType;
    }

    private static string BuildPrompt(string goal, string observation) =>
        "You operate a bank back-office UI. Goal: " + goal + "\nObservation:\n" + observation +
        "\nReply with ONE JSON object only. Tools: navigate, click, type, extract, checkpoint, finish." +
        "\nExamples: {\"tool\":\"type\",\"css\":\"input[name=memberno]\",\"value\":\"12345\",\"parameter\":\"memberId\"}" +
        " {\"tool\":\"click\",\"text\":\"Lookup\",\"css\":\"button\"}" +
        " {\"tool\":\"checkpoint\",\"textContains\":\"Member record\"}" +
        " {\"tool\":\"extract\",\"css\":\"td\",\"extractName\":\"balance\",\"outputType\":\"decimal\"}" +
        " {\"tool\":\"finish\"}." +
        " Use parameter only for memberId or baseUrl. Do not invent other parameter names.";

    /// <summary>
    /// Deterministic fixture/reference artifact for tests, <c>--scripted</c> demos, and offline replay.
    /// Not used as the successful output of <see cref="DiscoverAsync"/>.
    /// </summary>
    public static CapabilityArtifact ScriptedLookup(string _) => new()
    {
        SchemaVersion = Constants.Schema.Version,
        Id = Constants.ArtifactId.LookupSavingsBalance,
        Description = "Look up a member by ID and extract current savings balance.",
        ArtifactVersion = 1,
        ApprovalState = Constants.Approval.Approved,
        Inputs =
        [
            new() { Name = Constants.Field.MemberId, Type = Constants.Field.StringType },
            new() { Name = Constants.Field.BaseUrl, Type = Constants.Field.StringType }
        ],
        Outputs = [new() { Name = Constants.Field.Balance, Type = Constants.Field.DecimalType }],
        KnownOutcomes = DemoBankEnvironmentKnowledge.KnownOutcomes(),
        RecoverableConditions = DemoBankEnvironmentKnowledge.RecoverableConditions(),
        Steps =
        [
            new() { Id = Constants.StepId.OpenHome, Action = Constants.Action.Navigate, Url = Constants.Template.BaseUrlRoot, Risk = Constants.Risk.ReadOnly },
            new()
            {
                Id = Constants.StepId.TypeId, Action = Constants.Action.Type, Value = Constants.Template.MemberId, Risk = Constants.Risk.Reversible,
                Locators = [new() { Strategy = Constants.Locator.Css, Value = Constants.Selector.MemberNumberInput }]
            },
            new()
            {
                Id = Constants.StepId.Submit, Action = Constants.Action.Click, Risk = Constants.Risk.Reversible,
                Locators =
                [
                    new() { Strategy = Constants.Locator.Role, Role = Constants.Ui.ButtonRole, Name = Constants.Ui.Lookup },
                    new() { Strategy = Constants.Locator.Text, Value = Constants.Ui.Lookup }
                ]
            },
            new()
            {
                Id = Constants.StepId.OpenMember, Action = Constants.Action.Click, Risk = Constants.Risk.ReadOnly,
                Locators = [new() { Strategy = Constants.Locator.Css, Value = Constants.Selector.TableLink }]
            },
            new()
            {
                Id = Constants.StepId.CheckpointMember, Action = Constants.Action.Checkpoint, TextContains = Constants.Ui.MemberRecord, Risk = Constants.Risk.ReadOnly
            },
            new()
            {
                Id = Constants.StepId.ExtractBalance, Action = Constants.Action.Extract, ExtractName = Constants.Field.Balance, Risk = Constants.Risk.ReadOnly,
                Locators = [new() { Strategy = Constants.Locator.Css, Value = Constants.Selector.SavingsCell }]
            }
        ]
    };

    /// <summary>
    /// Deterministic fixture for the HITL sub-account confirm path. Not LLM discovery output.
    /// </summary>
    public static CapabilityArtifact ScriptedSubAccount()
    {
        var a = ScriptedLookup("");
        a.Id = Constants.ArtifactId.OpenSubAccount;
        a.Description = "Start opening a sub-account (risky confirm).";
        a.Outputs = [];
        a.Steps.Add(new ArtifactStep
        {
            Id = Constants.StepId.OpenSub,
            Action = Constants.Action.Click,
            Risk = Constants.Risk.Risky,
            Locators = [new() { Strategy = Constants.Locator.Text, Value = Constants.Ui.OpenSubAccount }]
        });
        a.Steps.Add(new ArtifactStep
        {
            Id = Constants.StepId.Confirm,
            Action = Constants.Action.Click,
            Risk = Constants.Risk.Irreversible,
            Locators = [new() { Strategy = Constants.Locator.Text, Value = Constants.Ui.ConfirmOpenSubAccount }]
        });
        return a;
    }
}
