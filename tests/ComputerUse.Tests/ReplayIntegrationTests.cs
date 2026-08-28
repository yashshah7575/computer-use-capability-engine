using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Replay;
using ComputerUse.Surfaces.Playwright;
using DemoBank;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace ComputerUse.Tests;

public class ReplayIntegrationTests : IAsyncLifetime
{
    private WebApplication? _app;
    private const string Url = "http://127.0.0.1:18510";

    public async Task InitializeAsync()
    {
        _app = DemoBankApp.Build([], Url);
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    [Fact]
    public async Task Replay_known_member_extracts_balance()
    {
        var allow = LoadAllow();
        allow.AllowedPorts.Add(18510);
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var result = await new ReplayEngine().RunAsync(
            DiscoveryAgent.ScriptedLookup(Url),
            new Dictionary<string, string> { ["memberId"] = "12345", ["baseUrl"] = Url },
            driver, allow, Path.Combine(Path.GetTempPath(), "cu-ok"));
        Assert.Equal(ResultKind.Success, result.Kind);
        Assert.Contains("1842", result.Outputs["balance"]);
    }

    [Fact]
    public async Task Replay_unknown_member_is_business_outcome()
    {
        var allow = LoadAllow();
        allow.AllowedPorts.Add(18510);
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var result = await new ReplayEngine().RunAsync(
            DiscoveryAgent.ScriptedLookup(Url),
            new Dictionary<string, string> { ["memberId"] = "00000", ["baseUrl"] = Url },
            driver, allow, Path.Combine(Path.GetTempPath(), "cu-nf"));
        Assert.Equal(ResultKind.BusinessOutcome, result.Kind);
    }

    [Fact]
    public async Task Replay_simulated_failure_is_hard_failure()
    {
        var allow = LoadAllow();
        allow.AllowedPorts.Add(18510);
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var result = await new ReplayEngine().RunAsync(
            DiscoveryAgent.ScriptedLookup(Url),
            new Dictionary<string, string> { ["memberId"] = "12345", ["baseUrl"] = Url },
            driver, allow, Path.Combine(Path.GetTempPath(), "cu-fail"),
            simulateLocatorFailure: true);
        Assert.Equal(ResultKind.HardFailure, result.Kind);
        Assert.False(string.IsNullOrEmpty(result.StepId));
    }

    private static AllowlistConfig LoadAllow() => new()
    {
        AllowedHosts = ["127.0.0.1", "localhost"],
        AllowedPorts = [5100, 18510],
        AllowedPathPrefixes = ["/"],
        AllowedActions = ["navigate", "click", "type", "extract", "checkpoint", "wait"]
    };
}
