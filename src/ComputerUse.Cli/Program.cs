using System.Text.Json;
using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Handoff;
using ComputerUse.Replay;
using ComputerUse.Surfaces.Playwright;

var repo = FindRepo(AppContext.BaseDirectory);
var allowlist = JsonSerializer.Deserialize<AllowlistConfig>(
    await File.ReadAllTextAsync(Path.Combine(repo, Constants.PathName.Config, Constants.PathName.Allowlist)),
    ArtifactSerializer.JsonOptions) ?? new AllowlistConfig();

if (args.Length == 0)
{
    Console.WriteLine($"Usage: {Constants.Cli.Discover} | {Constants.Cli.Replay} | {Constants.Cli.Hitl} | {Constants.Cli.CaptureDemo} | {Constants.Cli.Approve} | {Constants.Cli.Stability}");
    return 1;
}

var headless = args[0] == Constants.Cli.Stability ? !args.Contains(Constants.Flag.Headed) : args.Contains(Constants.Flag.Headless);
var cmd = args[0];

return cmd switch
{
    Constants.Cli.Discover => await Discover(),
    Constants.Cli.Replay => await Replay(),
    Constants.Cli.Hitl => await Hitl(),
    Constants.Cli.CaptureDemo => await CaptureDemo(),
    Constants.Cli.Approve => await Approve(),
    Constants.Cli.Stability => await Stability(),
    _ => Fail("unknown command")
};

async Task<int> Discover()
{
    var goal = Get(args, Constants.Flag.Goal) ?? $"look up member {Constants.Member.Known} and read their current savings balance";
    var url = Get(args, Constants.Flag.Url) ?? Constants.Network.DemoBankUrl;
    var scripted = args.Contains(Constants.Flag.Scripted);
    var artifactPath = Path.Combine(repo, Constants.PathName.Artifacts, Constants.ArtifactId.LookupArtifactFile);
    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);

    if (scripted)
    {
        var a = DiscoveryAgent.ScriptedLookup(url);
        await File.WriteAllTextAsync(artifactPath, ArtifactSerializer.Serialize(a));
        Console.WriteLine($"Wrote scripted fixture {artifactPath} (not LLM discovery).");
        return 0;
    }

    var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    var evidence = Path.Combine(repo, Constants.PathName.Evidence, Constants.PathName.Discovery, runId);
    Directory.CreateDirectory(evidence);

    await using var driver = await PlaywrightDriver.LaunchAsync(headless);
    var member = Get(args, Constants.Flag.MemberId);
    var agent = new DiscoveryAgent(new BedrockLanguageModel());
    try
    {
        var context = DiscoveryContext.From(goal, url, member);
        var artifact = await agent.DiscoverAsync(context, driver, allowlist, evidence);
        await File.WriteAllTextAsync(artifactPath, ArtifactSerializer.Serialize(artifact));
        await File.WriteAllTextAsync(Path.Combine(evidence, Constants.PathName.ArtifactJson), ArtifactSerializer.Serialize(artifact));
        var summary = new ExecutionResult
        {
            Kind = ResultKind.Success,
            Message = "Discovery emitted draft artifact.",
            EvidenceDir = evidence
        };
        await File.WriteAllTextAsync(
            Path.Combine(evidence, Constants.PathName.Result),
            JsonSerializer.Serialize(summary, ArtifactSerializer.JsonOptions));
        Console.WriteLine($"Discovery complete. Draft artifact: {artifactPath}");
        Console.WriteLine($"Evidence: {evidence}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(Redaction.Redact(ex.Message));
        await driver.ScreenshotAsync(Path.Combine(evidence, Constants.PathName.FailureScreenshot));
        return 2;
    }
}

async Task<int> Replay()
{
    var artifactPath = Get(args, Constants.Flag.Artifact) ?? Path.Combine(repo, Constants.PathName.Artifacts, Constants.ArtifactId.LookupArtifactFile);
    var member = Get(args, Constants.Flag.MemberId) ?? Constants.Member.Known;
    var url = Get(args, Constants.Flag.Url) ?? Constants.Network.DemoBankUrl;
    var simulate = args.Contains(Constants.Flag.SimulateFailure);
    var allowDraft = args.Contains(Constants.Flag.AllowDraft);
    var evidence = Path.Combine(repo, Constants.PathName.Evidence, "replay-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
    var artifact = ArtifactSerializer.Deserialize(await File.ReadAllTextAsync(artifactPath));
    var inputs = new Dictionary<string, string> { [Constants.Field.MemberId] = member, [Constants.Field.BaseUrl] = url.TrimEnd('/') };

    await using var driver = await PlaywrightDriver.LaunchAsync(headless);
    await using var handoff = new HandoffHost();
    await handoff.StartAsync();
    IReplayEngine engine = new ReplayEngine();
    var result = await engine.RunAsync(new ReplayRequest
    {
        Artifact = artifact,
        Inputs = inputs,
        Surface = driver,
        Allowlist = allowlist,
        EvidenceDir = evidence,
        SimulateLocatorFailure = simulate,
        AllowDraft = allowDraft,
        OnHumanGate = async step =>
        {
            var shot = Path.Combine(evidence, "intervention.png");
            await driver.ScreenshotAsync(shot);
            await driver.StartHumanAuditAsync();
            var outcome = await handoff.WaitForHumanAsync(new InterventionRequest
            {
                RunId = Path.GetFileName(evidence),
                StepId = step.Id,
                Reason = $"Risk {step.Risk} requires an explicit human decision.",
                ScreenshotPath = shot
            }, driver.PeekHumanAuditAsync);
            return outcome;
        },
        ResumeAutomation = () => handoff.ResumeAutomation()
    });

    Console.WriteLine(JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));
    await File.WriteAllTextAsync(Path.Combine(evidence, Constants.PathName.Result), JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));
    return result.Kind is ResultKind.Success or ResultKind.BusinessOutcome or ResultKind.Recoverable ? 0 : 3;
}

async Task<int> CaptureDemo()
{
    var url = (Get(args, Constants.Flag.Url) ?? Constants.Network.DemoBankUrl).TrimEnd('/');
    var dir = Path.Combine(repo, Constants.PathName.Evidence, "demo-captures");
    Directory.CreateDirectory(dir);
    await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);

    await driver.NavigateAsync(url + "/");
    await driver.ScreenshotAsync(Path.Combine(dir, "01-home.png"));

    await driver.TypeAsync([new LocatorSpec { Strategy = Constants.Locator.Css, Value = Constants.Selector.MemberNumberInput }], Constants.Member.Known);
    await driver.ClickAsync(
    [
        new LocatorSpec { Strategy = Constants.Locator.Role, Role = Constants.Ui.ButtonRole, Name = Constants.Ui.Lookup },
        new LocatorSpec { Strategy = Constants.Locator.Text, Value = Constants.Ui.Lookup }
    ]);
    await driver.ScreenshotAsync(Path.Combine(dir, "02-search-results.png"));

    await driver.ClickAsync([new LocatorSpec { Strategy = Constants.Locator.Css, Value = Constants.Selector.MemberHref }]);
    await driver.ScreenshotAsync(Path.Combine(dir, "03-member-record.png"));

    await driver.ClickAsync([new LocatorSpec { Strategy = Constants.Locator.Text, Value = Constants.Ui.OpenSubAccount }]);
    await driver.ScreenshotAsync(Path.Combine(dir, "04-subaccount-confirm.png"));

    await driver.NavigateAsync(url + Constants.Route.LookupQueryUnknown);
    await driver.ScreenshotAsync(Path.Combine(dir, "05-not-found.png"));

    var evidenceOk = Path.Combine(dir, "tmp-ok");
    var result = await new ReplayEngine().RunAsync(
        DiscoveryAgent.ScriptedLookup(url),
        new Dictionary<string, string> { [Constants.Field.MemberId] = Constants.Member.Known, [Constants.Field.BaseUrl] = url },
        driver, allowlist, evidenceOk);
    await driver.ScreenshotAsync(Path.Combine(dir, "06-replay-success.png"));
    await File.WriteAllTextAsync(Path.Combine(dir, "06-replay-success.json"),
        JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));

    var evidenceFail = Path.Combine(dir, "tmp-fail");
    var fail = await new ReplayEngine().RunAsync(
        DiscoveryAgent.ScriptedLookup(url),
        new Dictionary<string, string> { [Constants.Field.MemberId] = Constants.Member.Known, [Constants.Field.BaseUrl] = url },
        driver, allowlist, evidenceFail, simulateLocatorFailure: true);
    if (File.Exists(Path.Combine(evidenceFail, Constants.PathName.FailureScreenshot)))
        File.Copy(Path.Combine(evidenceFail, Constants.PathName.FailureScreenshot), Path.Combine(dir, "07-hard-failure.png"), true);
    await File.WriteAllTextAsync(Path.Combine(dir, "07-hard-failure.json"),
        JsonSerializer.Serialize(fail, ArtifactSerializer.JsonOptions));

    await using var handoff = new HandoffHost();
    await handoff.StartAsync(Constants.Network.CaptureDemoOperatorPort);
    var shotPath = Path.Combine(dir, "08-session-before-hitl.png");
    await driver.NavigateAsync(url + Constants.Route.SubAccountQueryKnown);
    await driver.ScreenshotAsync(shotPath);
    handoff.Preview(new InterventionRequest
    {
        RunId = "demo-hitl",
        StepId = Constants.StepId.Confirm,
        Reason = $"{Constants.Risk.Irreversible} confirm open sub-account requires a human.",
        ScreenshotPath = shotPath,
        Controller = ControllerKind.Human
    });
    await driver.NavigateAsync(Constants.Network.LoopbackUrl(Constants.Network.CaptureDemoOperatorPort) + "/");
    await driver.ScreenshotAsync(Path.Combine(dir, "08-operator-hitl.png"));

    foreach (var tmp in new[] { evidenceOk, evidenceFail })
    {
        try { Directory.Delete(tmp, true); } catch { /* ignore */ }
    }

    Console.WriteLine($"Wrote screenshots under {dir}");
    return 0;
}

async Task<int> Hitl()
{
    var path = Path.Combine(repo, Constants.PathName.Artifacts, Constants.ArtifactId.SubAccountArtifactFile);
    if (!File.Exists(path))
        await File.WriteAllTextAsync(path, ArtifactSerializer.Serialize(DiscoveryAgent.ScriptedSubAccount()));
    args = [Constants.Cli.Replay, Constants.Flag.Artifact, path, Constants.Flag.MemberId, Constants.Member.Known];
    return await Replay();
}

async Task<int> Approve()
{
    var artifactPath = Get(args, Constants.Flag.Artifact) ?? Path.Combine(repo, Constants.PathName.Artifacts, Constants.ArtifactId.LookupArtifactFile);
    var artifact = ArtifactSerializer.Deserialize(await File.ReadAllTextAsync(artifactPath));
    artifact.ApprovalState = Constants.Approval.Approved;
    await File.WriteAllTextAsync(artifactPath, ArtifactSerializer.Serialize(artifact));
    Console.WriteLine($"Approved {artifact.Id} at {artifactPath}");
    return 0;
}

async Task<int> Stability()
{
    var runs = int.TryParse(Get(args, Constants.Flag.Runs), out var n) && n > 0 ? n : Constants.Timing.DefaultStabilityRuns;
    var artifactPath = Get(args, Constants.Flag.Artifact) ?? Path.Combine(repo, Constants.PathName.Artifacts, Constants.ArtifactId.LookupArtifactFile);
    var member = Get(args, Constants.Flag.MemberId) ?? Constants.Member.Known;
    var url = Get(args, Constants.Flag.Url) ?? Constants.Network.DemoBankUrl;
    var allowDraft = args.Contains(Constants.Flag.AllowDraft);
    var evidenceRoot = Path.Combine(repo, Constants.PathName.Evidence, "stability-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
    Directory.CreateDirectory(evidenceRoot);
    var artifact = ArtifactSerializer.Deserialize(await File.ReadAllTextAsync(artifactPath));
    var inputs = new Dictionary<string, string> { [Constants.Field.MemberId] = member, [Constants.Field.BaseUrl] = url.TrimEnd('/') };
    var results = new List<ExecutionResult>();
    for (var i = 0; i < runs; i++)
    {
        var dir = Path.Combine(evidenceRoot, $"run-{i + 1:00}");
        await using var driver = await PlaywrightDriver.LaunchAsync(headless);
        var result = await new ReplayEngine().RunAsync(artifact, inputs, driver, allowlist, dir, allowDraft: allowDraft);
        await File.WriteAllTextAsync(
            Path.Combine(dir, Constants.PathName.Result),
            JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));
        results.Add(result);
    }

    var report = StabilityReport.From(results);
    var reportPath = Path.Combine(evidenceRoot, "report.json");
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, ArtifactSerializer.JsonOptions));
    Console.WriteLine(JsonSerializer.Serialize(report, ArtifactSerializer.JsonOptions));
    Console.WriteLine($"Wrote {reportPath}");
    return report.PassRate >= 1.0 ? 0 : 3;
}

static int Fail(string m) { Console.Error.WriteLine(m); return 1; }

static string? Get(string[] a, string flag)
{
    var i = Array.IndexOf(a, flag);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

static string FindRepo(string start)
{
    var d = new DirectoryInfo(start);
    while (d is not null)
    {
        if (File.Exists(Path.Combine(d.FullName, Constants.PathName.Solution)))
            return d.FullName;
        d = d.Parent;
    }
    return Directory.GetCurrentDirectory();
}
