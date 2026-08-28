using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class ObservationFormatterTests
{
    [Fact]
    public void Format_InteractiveControl_IncludesSemanticFieldsWithoutDomDump()
    {
        // Arrange
        var controls = new List<ObservedControl>
        {
            new()
            {
                Tag = "input",
                Role = "textbox",
                Label = "Member Number",
                InputName = "memberno",
                Placeholder = "Member number"
            },
            new()
            {
                Tag = "button",
                Role = "button",
                Name = "Lookup",
                Text = "Lookup"
            }
        };

        // Act
        var actual = ObservationFormatter.Format(
            Constants.Network.DemoBankUrl + "/",
            "DemoBank",
            "Member Lookup\nEnter Member Number",
            controls);

        // Assert
        Assert.Contains("PAGE", actual);
        Assert.Contains("VISIBLE TEXT", actual);
        Assert.Contains("CONTROLS", actual);
        Assert.Contains("role=textbox", actual);
        Assert.Contains("label=Member Number", actual);
        Assert.Contains("placeholder=Member number", actual);
        Assert.Contains("nameAttr=memberno", actual);
        Assert.Contains("role=button", actual);
        Assert.DoesNotContain("<html", actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("innerHTML", actual);
    }

    [Fact]
    public void Parse_ControlBlock_ReadsNameAttrAndRole()
    {
        // Arrange
        var observation = """
            CONTROLS
            [0]
            tag=input
            role=textbox
            nameAttr=memberno
            label=Member number
            """;

        // Act
        var actual = ObservationControlParser.Parse(observation);

        // Assert
        var control = Assert.Single(actual);
        Assert.Equal("input", control.Tag);
        Assert.Equal("textbox", control.Role);
        Assert.Equal("memberno", control.InputName);
        Assert.Equal("Member number", control.Label);
    }
}
