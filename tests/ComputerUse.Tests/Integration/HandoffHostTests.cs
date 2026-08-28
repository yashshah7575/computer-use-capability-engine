using System.Net;
using ComputerUse.Domain;
using ComputerUse.Handoff;
using Xunit;

namespace ComputerUse.Tests.Integration;

public class HandoffHostTests
{
    private const int Port = Constants.Network.TestOperatorPort;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WaitForHumanAsync_ResumeWithoutActions_ReturnsConflict()
    {
        // Arrange
        await using var host = new HandoffHost();
        await host.StartAsync(Port);
        var actions = new List<HumanAction>();
        var wait = host.WaitForHumanAsync(
            new InterventionRequest { RunId = "t", StepId = Constants.StepId.Confirm, Reason = "test" },
            () => Task.FromResult<IReadOnlyList<HumanAction>>(actions));
        using var client = new HttpClient { BaseAddress = new Uri(Constants.Network.LoopbackUrl(Port)) };

        // Act
        var actual = await client.PostAsync(Constants.Route.Resume, new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, actual.StatusCode);

        actions.Add(new HumanAction { Kind = Constants.Action.Click, Detail = "Confirm" });
        await client.PostAsync(Constants.Route.Resume, new StringContent(""));
        await wait;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WaitForHumanAsync_ResumeAfterAction_CompletesWithHumanActions()
    {
        // Arrange
        await using var host = new HandoffHost();
        await host.StartAsync(Constants.Network.TestOperatorPortAlt);
        var actions = new List<HumanAction> { new() { Kind = Constants.Action.Click, Detail = "Confirm" } };
        var wait = host.WaitForHumanAsync(
            new InterventionRequest { RunId = "t", StepId = Constants.StepId.Confirm, Reason = "test" },
            () => Task.FromResult<IReadOnlyList<HumanAction>>(actions));
        using var client = new HttpClient { BaseAddress = new Uri(Constants.Network.LoopbackUrl(Constants.Network.TestOperatorPortAlt)) };

        // Act
        var response = await client.PostAsync(Constants.Route.Resume, new StringContent(""));
        var actual = await wait;

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Single(actual);
        Assert.Equal(Constants.Action.Click, actual[0].Kind);
    }
}
