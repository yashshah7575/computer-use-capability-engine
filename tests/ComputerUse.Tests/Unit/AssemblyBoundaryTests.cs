using ComputerUse.Replay;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class AssemblyBoundaryTests
{
    [Fact]
    public void ReplayAssembly_DoesNotReferencePlaywright()
    {
        // Arrange
        var names = typeof(ReplayEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        // Act
        var referencesPlaywright = names.Contains("Microsoft.Playwright");

        // Assert
        Assert.False(referencesPlaywright);
    }
}
