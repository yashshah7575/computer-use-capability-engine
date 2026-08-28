using ComputerUse.Domain;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class StabilityReportTests
{
    [Fact]
    public void From_MixedResults_ComputesPassRateAndDegradations()
    {
        // Arrange
        ExecutionResult[] results =
        [
            new() { Kind = ResultKind.Success },
            new() { Kind = ResultKind.Recoverable, Degradations = [new Degradation { Kind = Constants.DegradationKind.TierDegraded }] },
            new() { Kind = ResultKind.HardFailure }
        ];

        // Act
        var actual = StabilityReport.From(results);

        // Assert
        Assert.Equal(3, actual.RunCount);
        Assert.Equal(1, actual.OutcomeCounts["Success"]);
        Assert.Equal(1, actual.OutcomeCounts["Recoverable"]);
        Assert.Equal(2.0 / 3.0, actual.PassRate, 5);
        Assert.True(actual.AnyDegradations);
    }
}
