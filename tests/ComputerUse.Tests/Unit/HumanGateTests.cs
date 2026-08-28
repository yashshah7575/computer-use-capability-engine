using ComputerUse.Domain;
using ComputerUse.Replay;
using ComputerUse.Tests.Fakes;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class HumanGateTests
{
    [Fact]
    public async Task RunAsync_RiskyStepWithoutGate_ReturnsInterventionRequired()
    {
        // Arrange
        var surface = new FakeSurfaceDriver();
        var request = RiskyRequest(surface, onHumanGate: null);

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.InterventionRequired, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
    }

    [Fact]
    public async Task RunAsync_HumanDenies_DoesNotExecuteRiskyClick()
    {
        // Arrange
        var surface = new FakeSurfaceDriver();
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.Denied,
            Actions = [new HumanAction { Kind = Constants.Action.Click, Detail = "unrelated" }]
        }));

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.InterventionRequired, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
        Assert.Contains("denied", actual.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_AuthorizeAutomation_ExecutesRiskyClickOnce()
    {
        // Arrange
        var surface = new FakeSurfaceDriver { PageText = "Done" };
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.AuthorizeAutomation
        }));

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Success, actual.Kind);
        Assert.Equal(1, surface.ClickCount);
    }

    [Fact]
    public async Task RunAsync_CompletedByHumanWhenTargetGone_DoesNotClick()
    {
        // Arrange
        var resumed = false;
        var surface = new FakeSurfaceDriver { CanResolve = false, PageText = "Done" };
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.CompletedByHuman
        }), () => resumed = true);

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Success, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
        Assert.True(resumed);
    }

    [Fact]
    public async Task RunAsync_CompletedByHumanUnverified_RemainsInterventionRequired()
    {
        // Arrange
        var resumed = false;
        var surface = new FakeSurfaceDriver { CanResolve = true, PageText = "still on confirm" };
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.CompletedByHuman
        }), () => resumed = true);

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.InterventionRequired, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
        Assert.False(resumed);
        Assert.Contains("could not be verified", actual.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_UnrelatedBrowserActionWithDeny_DoesNotAuthorize()
    {
        // Arrange
        var surface = new FakeSurfaceDriver();
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.Denied,
            Actions = [new HumanAction { Kind = Constants.Action.Click, Detail = "random" }]
        }));

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.InterventionRequired, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
    }

    [Fact]
    public async Task RunAsync_CompletedByHumanWhenNextCheckpointPresent_DoesNotClick()
    {
        // Arrange
        var surface = new FakeSurfaceDriver { CanResolve = true, PageText = "Done" };
        var request = RiskyRequest(surface, _ => Task.FromResult(new HumanGateOutcome
        {
            Decision = HumanGateDecision.CompletedByHuman
        }));

        // Act
        var actual = await new ReplayEngine().RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.Success, actual.Kind);
        Assert.Equal(0, surface.ClickCount);
    }

    private static ReplayRequest RiskyRequest(
        FakeSurfaceDriver surface,
        Func<ArtifactStep, Task<HumanGateOutcome>>? onHumanGate,
        Action? resumeAutomation = null)
    {
        var artifact = new CapabilityArtifact
        {
            SchemaVersion = Constants.Schema.Version,
            Id = "risky-confirm",
            ApprovalState = Constants.Approval.Approved,
            Steps =
            [
                new ArtifactStep
                {
                    Id = Constants.StepId.Confirm,
                    Action = Constants.Action.Click,
                    Risk = Constants.Risk.Irreversible,
                    Locators = [new LocatorSpec { Strategy = Constants.Locator.Text, Value = "Confirm" }]
                },
                new ArtifactStep
                {
                    Id = "after",
                    Action = Constants.Action.Checkpoint,
                    TextContains = "Done",
                    Risk = Constants.Risk.ReadOnly
                }
            ]
        };
        return new ReplayRequest
        {
            Artifact = artifact,
            Inputs = new Dictionary<string, string>(),
            Surface = surface,
            Allowlist = new AllowlistConfig
            {
                AllowedHosts = [Constants.Network.Loopback],
                AllowedPorts = [Constants.Network.DemoBankPort],
                AllowedPathPrefixes = ["/"],
                AllowedActions = [Constants.Action.Click, Constants.Action.Checkpoint]
            },
            EvidenceDir = Path.Combine(Path.GetTempPath(), "cu-hitl-" + Guid.NewGuid().ToString("N")),
            OnHumanGate = onHumanGate,
            ResumeAutomation = resumeAutomation
        };
    }
}
