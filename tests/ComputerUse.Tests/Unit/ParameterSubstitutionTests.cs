using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class ParameterSubstitutionTests
{
    [Fact]
    public void Apply_Templates_SubstitutesAllKeys()
    {
        // Arrange
        const string template = Constants.Template.BaseUrlRoot + "x/" + Constants.Template.MemberId;
        var values = new Dictionary<string, string>
        {
            [Constants.Field.BaseUrl] = Constants.Network.DemoBankUrl,
            [Constants.Field.MemberId] = Constants.Member.Known
        };

        // Act
        var actual = ParameterSubstitution.Apply(template, values);

        // Assert
        Assert.Equal($"{Constants.Network.DemoBankUrl}/x/{Constants.Member.Known}", actual);
    }
}
