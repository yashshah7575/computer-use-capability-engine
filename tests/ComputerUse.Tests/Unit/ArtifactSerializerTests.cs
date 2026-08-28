using ComputerUse.Agent;
using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class ArtifactSerializerTests
{
    [Fact]
    public void Serialize_ScriptedLookup_RoundTripsIdAndSteps()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(Constants.Network.DemoBankUrl);

        // Act
        var actual = ArtifactSerializer.Deserialize(ArtifactSerializer.Serialize(artifact));

        // Assert
        Assert.Equal(artifact.Id, actual.Id);
        Assert.Equal(artifact.Steps.Count, actual.Steps.Count);
        Assert.Equal(Constants.Approval.Approved, actual.ApprovalState);
        Assert.Contains(actual.KnownOutcomes, o => o.Code == Constants.Outcome.MemberNotFound);
    }

    [Fact]
    public void Validate_KnownOutcomeMissingCode_Throws()
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(Constants.Network.DemoBankUrl);
        artifact.KnownOutcomes.Add(new KnownOutcome { TextContains = "x" });

        // Act
        var act = () => ArtifactSerializer.Validate(artifact);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Theory]
    [InlineData(Constants.ArtifactId.LookupSavingsBalance)]
    [InlineData("cap-1")]
    public void Deserialize_SerializedId_PreservesId(string id)
    {
        // Arrange
        var artifact = DiscoveryAgent.ScriptedLookup(Constants.Network.DemoBankUrl);
        artifact.Id = id;

        // Act
        var actual = ArtifactSerializer.Deserialize(ArtifactSerializer.Serialize(artifact));

        // Assert
        Assert.Equal(id, actual.Id);
    }
}
