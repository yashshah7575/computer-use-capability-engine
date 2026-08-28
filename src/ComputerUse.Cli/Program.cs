using System.Text.Json;
using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Handoff;
using ComputerUse.Replay;
using ComputerUse.Surfaces.Playwright;

var repo = FindRepo(AppContext.BaseDirectory);
var allowlist = JsonSerializer.Deserialize<AllowlistConfig>(
    await File.ReadAllTextAsync(Path.Combine(repo, "config", "allowlist.json")),
    ArtifactSerializer.JsonOptions) ?? new AllowlistConfig();

if (args.Length == 0)
{
    Console.WriteLine("Usage: discover | replay | hitl | capture-demo");
    return 1;
}

var headless = args.Contains("--headless");
var cmd = args[0];

return cmd switch
{
    "discover" => await Discover(),
    "replay" => await Replay(),
    "hitl" => await Hitl(),
    "capture-demo" => await CaptureDemo(),
    _ => Fail("unknown command")
};

async Task<int> Discover()
{
    var goal = Get(args, "--goal") ?? "look up member 12345 and read their current savings balance";
    var url = Get(args, "--url") ?? "http://127.0.0.1:5100";
    var scripted = args.Contains("--scripted");
    var evidence = Path.Combine(repo, "evidence", "discovery-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
    Directory.CreateDirectory(evidence);
    var artifactPath = Path.Combine(repo, "artifacts", "lookup-savings-balance.json");
    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);

    if (scripted)
    {
        var a = DiscoveryAgent.ScriptedLookup(url);
        await File.WriteAllTextAsync(artifactPath, ArtifactSerializer.Serialize(a));
        Console.WriteLine($"Wrote scripted artifact {artifactPath}");
        return 0;
    }

    await using var driver = await PlaywrightDriver.LaunchAsync(headless);
    var agent = new DiscoveryAgent();
    try
    {
        var artifact = await agent.DiscoverAsync(goal, url, driver, allowlist, evidence);
        await File.WriteAllTextAsync(artifactPath, ArtifactSerializer.Serialize(artifact));
        await File.WriteAllTextAsync(Path.Combine(evidence, "artifact.json"), ArtifactSerializer.Serialize(artifact));
        Console.WriteLine($"Discovery complete. Artifact: {artifactPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(Redaction.Redact(ex.Message));
        await driver.ScreenshotAsync(Path.Combine(evidence, "failure.png"));
        return 2;
    }
}

async Task<int> Replay()
{
    var artifactPath = Get(args, "--artifact") ?? Path.Combine(repo, "artifacts", "lookup-savings-balance.json");
    var member = Get(args, "--member-id") ?? "12345";
    var url = Get(args, "--url") ?? "http://127.0.0.1:5100";
    var simulate = args.Contains("--simulate-failure");
    var evidence = Path.Combine(repo, "evidence", "replay-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
    var artifact = ArtifactSerializer.Deserialize(await File.ReadAllTextAsync(artifactPath));
    var inputs = new Dictionary<string, string> { ["memberId"] = member, ["baseUrl"] = url.TrimEnd('/') };

    await using var driver = await PlaywrightDriver.LaunchAsync(headless);
    await using var handoff = new HandoffHost();
    await handoff.StartAsync();
    var engine = new ReplayEngine();
    var result = await engine.RunAsync(artifact, inputs, driver, allowlist, evidence, simulate,
        async step =>
        {
            var shot = Path.Combine(evidence, "intervention.png");
            await driver.ScreenshotAsync(shot);
            await handoff.WaitForHumanAsync(new InterventionRequest
            {
                RunId = Path.GetFileName(evidence),
                StepId = step.Id,
                Reason = $"Risk {step.Risk} requires human confirmation.",
                ScreenshotPath = shot
            });
            return true;
        });

    Console.WriteLine(JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));
    await File.WriteAllTextAsync(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));
    return result.Kind is ResultKind.Success or ResultKind.BusinessOutcome ? 0 : 3;
}

async Task<int> CaptureDemo()
{
    var url = (Get(args, "--url") ?? "http://127.0.0.1:5100").TrimEnd('/');
    var dir = Path.Combine(repo, "evidence", "demo-captures");
    Directory.CreateDirectory(dir);
    await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);

    await driver.NavigateAsync(url + "/");
    await driver.ScreenshotAsync(Path.Combine(dir, "01-home.png"));

    await driver.TypeAsync([new LocatorSpec { Strategy = "css", Value = "input[name=memberno]" }], "12345");
    await driver.ClickAsync(
    [
        new LocatorSpec { Strategy = "role", Role = "button", Name = "Lookup" },
        new LocatorSpec { Strategy = "text", Value = "Lookup" }
    ]);
    await driver.ScreenshotAsync(Path.Combine(dir, "02-search-results.png"));

    await driver.ClickAsync([new LocatorSpec { Strategy = "css", Value = "a[href*='member']" }]);
    await driver.ScreenshotAsync(Path.Combine(dir, "03-member-record.png"));

    await driver.ClickAsync([new LocatorSpec { Strategy = "text", Value = "Open sub-account" }]);
    await driver.ScreenshotAsync(Path.Combine(dir, "04-subaccount-confirm.png"));

    await driver.NavigateAsync(url + "/lookup?memberno=00000");
    await driver.ScreenshotAsync(Path.Combine(dir, "05-not-found.png"));

    var evidenceOk = Path.Combine(dir, "tmp-ok");
    var result = await new ReplayEngine().RunAsync(
        DiscoveryAgent.ScriptedLookup(url),
        new Dictionary<string, string> { ["memberId"] = "12345", ["baseUrl"] = url },
        driver, allowlist, evidenceOk);
    await driver.ScreenshotAsync(Path.Combine(dir, "06-replay-success.png"));
    await File.WriteAllTextAsync(Path.Combine(dir, "06-replay-success.json"),
        JsonSerializer.Serialize(result, ArtifactSerializer.JsonOptions));

    var evidenceFail = Path.Combine(dir, "tmp-fail");
    var fail = await new ReplayEngine().RunAsync(
        DiscoveryAgent.ScriptedLookup(url),
        new Dictionary<string, string> { ["memberId"] = "12345", ["baseUrl"] = url },
        driver, allowlist, evidenceFail, simulateLocatorFailure: true);
    if (File.Exists(Path.Combine(evidenceFail, "failure.png")))
        File.Copy(Path.Combine(evidenceFail, "failure.png"), Path.Combine(dir, "07-hard-failure.png"), true);
    await File.WriteAllTextAsync(Path.Combine(dir, "07-hard-failure.json"),
        JsonSerializer.Serialize(fail, ArtifactSerializer.JsonOptions));

    await using var handoff = new HandoffHost();
    await handoff.StartAsync(5201);
    var shotPath = Path.Combine(dir, "08-session-before-hitl.png");
    await driver.NavigateAsync(url + "/subaccount?id=12345");
    await driver.ScreenshotAsync(shotPath);
    handoff.Preview(new InterventionRequest
    {
        RunId = "demo-hitl",
        StepId = "confirm",
        Reason = "IRREVERSIBLE confirm open sub-account requires a human.",
        ScreenshotPath = shotPath,
        Controller = ControllerKind.Human
    });
    await driver.NavigateAsync("http://127.0.0.1:5201/");
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
    var path = Path.Combine(repo, "artifacts", "open-sub-account.json");
    if (!File.Exists(path))
        await File.WriteAllTextAsync(path, ArtifactSerializer.Serialize(DiscoveryAgent.ScriptedSubAccount()));
    args = ["replay", "--artifact", path, "--member-id", "12345"];
    return await Replay();
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
        if (File.Exists(Path.Combine(d.FullName, "ComputerUse.sln")))
            return d.FullName;
        d = d.Parent;
    }
    return Directory.GetCurrentDirectory();
}
