using ComputerUse.Agent;
using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class PolicyEngineTests
{
    private static AllowlistConfig AllowLocal() => new()
    {
        AllowedHosts = [Constants.Network.Loopback],
        AllowedPorts = [Constants.Network.DemoBankPort],
        AllowedPathPrefixes = ["/"],
        AllowedActions = [Constants.Action.Navigate]
    };

    [Fact]
    public void CheckAction_UnlistedHost_ReturnsPolicyFailure()
    {
        // Arrange
        var config = AllowLocal();
        var remote = new Uri("https://evil.example/");

        // Act
        var actual = PolicyEngine.CheckAction(config, Constants.Action.Navigate, remote);

        // Assert
        Assert.Equal(ResultKind.PolicyFailure, actual!.Kind);
    }

    [Fact]
    public void RequiresHuman_Risky_ReturnsTrue()
    {
        // Arrange
        const RiskClass risk = RiskClass.Risky;

        // Act
        var actual = PolicyEngine.RequiresHuman(risk);

        // Assert
        Assert.True(actual);
    }

    [Fact]
    public void RequiresHuman_ReadOnly_ReturnsFalse()
    {
        // Arrange
        const RiskClass risk = RiskClass.ReadOnly;

        // Act
        var actual = PolicyEngine.RequiresHuman(risk);

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public void CheckApproval_DraftWithIrreversibleStep_ReturnsPolicyFailure()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedSubAccount();
        artifact.ApprovalState = Constants.Approval.Draft;

        // Act
        var actual = PolicyEngine.CheckApproval(artifact, allowDraft: false);

        // Assert
        Assert.Equal(ResultKind.PolicyFailure, actual!.Kind);
    }

    [Fact]
    public void CheckApproval_DraftWithAllowDraft_ReturnsNull()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedSubAccount();
        artifact.ApprovalState = Constants.Approval.Draft;

        // Act
        var actual = PolicyEngine.CheckApproval(artifact, allowDraft: true);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void CheckApproval_DraftReadOnlyLookup_ReturnsNull()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(Constants.Network.DemoBankUrl);
        artifact.ApprovalState = Constants.Approval.Draft;

        // Act
        var actual = PolicyEngine.CheckApproval(artifact, allowDraft: false);

        // Assert
        Assert.Null(actual);
    }
}
