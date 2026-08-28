using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Replay;
using ComputerUse.Surfaces.Playwright;
using DemoBank;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace ComputerUse.Tests.Integration;

public class ReplayEngineIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl = Constants.Network.TestDemoBankUrl;
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        _app = DemoBankApp.Build([], BaseUrl);
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_KnownMember_ReturnsBalance()
    {
        // Arrange
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Known, driver, "cu-ok");

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Success, actual.Kind);
        Assert.Contains("1842", actual.Outputs[Constants.Field.Balance]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_UnknownMember_ReturnsDeclaredBusinessOutcome()
    {
        // Arrange
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Unknown, driver, "cu-nf");

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.BusinessOutcome, actual.Kind);
        Assert.Equal(Constants.Outcome.MemberNotFound, actual.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_UnknownMemberWithoutMatchingOutcome_ReturnsHardFailure()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(BaseUrl);
        artifact.KnownOutcomes[0].TextContains = "ZZZ_NOT_A_MATCH";
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Unknown, driver, "cu-nf-hard", artifact);

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.HardFailure, actual.Kind);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_TransientMember_ReturnsRecoverableWithBalance()
    {
        // Arrange
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Transient, driver, "cu-rec");

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Recoverable, actual.Kind);
        Assert.Contains("500", actual.Outputs[Constants.Field.Balance]);
        Assert.Contains(actual.RecoveryEvents, e => e.Code == Constants.Outcome.TransientInterruption);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_FirstLocatorDead_RecordsTierDegradation()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(BaseUrl);
        artifact.Steps.First(s => s.Id == Constants.StepId.TypeId).Locators.Insert(0,
            new LocatorSpec { Strategy = Constants.Locator.Css, Value = Constants.Selector.DeadLocator });
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Known, driver, "cu-deg", artifact);

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Success, actual.Kind);
        Assert.Contains(actual.Degradations, d => d.Kind == Constants.DegradationKind.TierDegraded && d.MatchedLocatorIndex == 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_SimulatedLocatorFailure_ReturnsHardFailure()
    {
        // Arrange
        await using var driver = await PlaywrightDriver.LaunchAsync(headless: true);
        var request = LookupRequest(Constants.Member.Known, driver, "cu-fail");
        request = new ReplayRequest
        {
            Artifact = request.Artifact,
            Inputs = request.Inputs,
            Surface = request.Surface,
            Allowlist = request.Allowlist,
            EvidenceDir = request.EvidenceDir,
            SimulateLocatorFailure = true
        };

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.HardFailure, actual.Kind);
        Assert.False(string.IsNullOrEmpty(actual.StepId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task From_TwoSuccessfulLookups_ReportsFullPassRate()
    {
        // Arrange
        await using var firstDriver = await PlaywrightDriver.LaunchAsync(headless: true);
        var first = await new ReplayEngine().RunAsync(LookupRequest(Constants.Member.Known, firstDriver, "cu-stab-0"));
        await using var secondDriver = await PlaywrightDriver.LaunchAsync(headless: true);
        var second = await new ReplayEngine().RunAsync(LookupRequest(Constants.Member.Known, secondDriver, "cu-stab-1"));

        // Act
        var actual = StabilityReport.From([first, second]);

        // Assert
        Assert.Equal(2, actual.RunCount);
        Assert.Equal(1.0, actual.PassRate);
        Assert.Equal(2, actual.OutcomeCounts["Success"]);
    }

    private static ReplayRequest LookupRequest(
        string memberId,
        ISurfaceDriver surface,
        string evidenceName,
        CapabilityArtifact? artifact = null) =>
        new()
        {
            Artifact = artifact ?? DiscoveryAgent.ScriptedLookup(BaseUrl),
            Inputs = new Dictionary<string, string> { [Constants.Field.MemberId] = memberId, [Constants.Field.BaseUrl] = BaseUrl },
            Surface = surface,
            Allowlist = new AllowlistConfig
            {
                AllowedHosts = [Constants.Network.Loopback, Constants.Network.Localhost],
                AllowedPorts = [Constants.Network.DemoBankPort, Constants.Network.TestDemoBankPort],
                AllowedPathPrefixes = ["/"],
                AllowedActions = [.. Constants.Action.ReplayAllowlist]
            },
            EvidenceDir = Path.Combine(Path.GetTempPath(), evidenceName)
        };
}
