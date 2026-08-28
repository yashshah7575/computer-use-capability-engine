using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class RedactionTests
{
    [Fact]
    public void Redact_SecretToken_ReturnsPlaceholder()
    {
        // Arrange
        const string input = "password=hunter2";

        // Act
        var actual = Redaction.Redact(input);

        // Assert
        Assert.Equal("[REDACTED]", actual);
    }

    [Fact]
    public void Redact_PlainText_ReturnsOriginal()
    {
        // Arrange
        const string input = "ok";

        // Act
        var actual = Redaction.Redact(input);

        // Assert
        Assert.Equal(input, actual);
    }
}
