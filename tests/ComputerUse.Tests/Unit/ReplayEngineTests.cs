using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Replay;
using ComputerUse.Tests.Fakes;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class ReplayEngineTests
{
    [Fact]
    public async Task RunAsync_DraftIrreversibleArtifact_ReturnsPolicyFailure()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedSubAccount();
        artifact.ApprovalState = Constants.Approval.Draft;
        IReplayEngine engine = new ReplayEngine();
        var request = new ReplayRequest
        {
            Artifact = artifact,
            Inputs = new Dictionary<string, string>
            {
                [Constants.Field.MemberId] = Constants.Member.Known,
                [Constants.Field.BaseUrl] = Constants.Network.DemoBankUrl
            },
            Surface = new FakeSurfaceDriver(),
            Allowlist = new AllowlistConfig(),
            EvidenceDir = Path.Combine(Path.GetTempPath(), "cu-unit-draft")
        };

        // Act
        var actual = await engine.RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.PolicyFailure, actual.Kind);
    }

    [Fact]
    public async Task RunAsync_FailedClickMatchingKnownOutcome_ReturnsBusinessOutcome()
    {
        // Arrange
        var artifact = new CapabilityArtifact
        {
            SchemaVersion = Constants.Schema.Version,
            Id = "lookup",
            ApprovalState = Constants.Approval.Approved,
            KnownOutcomes = [new KnownOutcome { Code = Constants.Outcome.MemberNotFound, TextContains = Constants.Outcome.MemberNotFoundText }],
            Steps =
            [
                new ArtifactStep
                {
                    Id = Constants.StepId.OpenMember,
                    Action = Constants.Action.Click,
                    Risk = Constants.Risk.ReadOnly,
                    Locators = [new LocatorSpec { Strategy = Constants.Locator.Css, Value = Constants.Selector.GenericLink }]
                }
            ]
        };
        var surface = new FakeSurfaceDriver
        {
            CurrentUrl = Constants.Network.DemoBankUrl + "/lookup",
            PageText = Constants.Outcome.MemberNotFoundText,
            ThrowOnAct = true
        };
        IReplayEngine engine = new ReplayEngine();
        var request = new ReplayRequest
        {
            Artifact = artifact,
            Inputs = new Dictionary<string, string>(),
            Surface = surface,
            Allowlist = new AllowlistConfig
            {
                AllowedHosts = [Constants.Network.Loopback],
                AllowedPorts = [Constants.Network.DemoBankPort],
                AllowedPathPrefixes = ["/"],
                AllowedActions = [Constants.Action.Click]
            },
            EvidenceDir = Path.Combine(Path.GetTempPath(), "cu-unit-outcome")
        };

        // Act
        var actual = await engine.RunAsync(request);

        // Assert
        Assert.Equal(ResultKind.BusinessOutcome, actual.Kind);
        Assert.Equal(Constants.Outcome.MemberNotFound, actual.Message);
    }
}
