using System.Text.Json;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

public sealed class DiscoveryAgent
{
    private readonly ILanguageModel _model;

    public DiscoveryAgent(ILanguageModel model) =>
        _model = model ?? throw new ArgumentNullException(nameof(model));

    public async Task<CapabilityArtifact> DiscoverAsync(
        string goal,
        string baseUrl,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        int maxSteps = Constants.Timing.DefaultMaxDiscoverySteps)
    {
        Directory.CreateDirectory(evidenceDir);
        await surface.NavigateAsync(baseUrl.TrimEnd('/') + "/");
        var outputs = new Dictionary<string, string>();

        for (var i = 0; i < maxSteps; i++)
        {
            var obs = await surface.ObserveAsync();
            await File.WriteAllTextAsync(Path.Combine(evidenceDir, $"obs-{i}.txt"), Redaction.Redact(obs));
            var prompt =
                "You operate a bank back-office UI. Goal: " + goal + "\nObservation:\n" + obs +
                "\nReply with ONE JSON object only, e.g. {\"tool\":\"click\",\"css\":\"...\",\"text\":\"...\"} or {\"tool\":\"finish\"}.";

            var text = await _model.CompleteAsync(prompt);
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                continue;
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;
            var tool = root.GetProperty(Constants.Llm.Tool).GetString();
            var css = root.TryGetProperty(Constants.Llm.Css, out var c) ? c.GetString() ?? "" : "";
            var t = root.TryGetProperty(Constants.Llm.Text, out var tx) ? tx.GetString() ?? "" : "";
            var loc = new List<LocatorSpec>();
            if (!string.IsNullOrEmpty(root.TryGetProperty(Constants.Llm.Text, out var t2) ? t2.GetString() : null)
                && tool == Constants.Action.Click)
                loc.Add(new LocatorSpec { Strategy = Constants.Locator.Text, Value = t });
            if (css.Length > 0)
                loc.Add(new LocatorSpec { Strategy = Constants.Locator.Css, Value = css });

            var denied = PolicyEngine.CheckAction(
                allowlist,
                tool == Constants.Action.Finish ? Constants.Action.Extract : tool ?? Constants.Action.Click,
                new Uri(await surface.UrlAsync()));
            if (denied is not null && tool != Constants.Action.Finish)
                throw new InvalidOperationException(denied.Message);

            switch (tool)
            {
                case Constants.Action.Click:
                    await surface.ClickAsync(loc);
                    break;
                case Constants.Action.Type:
                    await surface.TypeAsync(loc, t);
                    break;
                case Constants.Action.Extract:
                    outputs[Constants.Field.Balance] = (await surface.ExtractAsync(loc)).Text;
                    break;
                case Constants.Action.Finish:
                    var draft = ScriptedLookup(baseUrl);
                    draft.ApprovalState = Constants.Approval.Draft;
                    return draft;
            }
        }

        if (outputs.Count > 0)
        {
            var partial = ScriptedLookup(baseUrl);
            partial.ApprovalState = Constants.Approval.Draft;
            return partial;
        }
        throw new InvalidOperationException("Discovery did not finish.");
    }

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
        KnownOutcomes =
        [
            new() { Code = Constants.Outcome.MemberNotFound, TextContains = Constants.Outcome.MemberNotFoundText }
        ],
        RecoverableConditions =
        [
            new()
            {
                Code = Constants.Outcome.TransientInterruption,
                TextContains = Constants.Outcome.InterruptionText,
                Action = Constants.Recovery.Dismiss,
                MaxRetries = 1,
                Locators = [new() { Strategy = Constants.Locator.Text, Value = Constants.Ui.Dismiss }]
            }
        ],
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
