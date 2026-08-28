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
    public async Task WaitForHumanAsync_AuthorizeWithoutBrowserAction_CompletesAuthorized()
    {
        // Arrange
        await using var host = new HandoffHost();
        await host.StartAsync(Port);
        var wait = host.WaitForHumanAsync(
            new InterventionRequest { RunId = "t", StepId = Constants.StepId.Confirm, Reason = "test" },
            () => Task.FromResult<IReadOnlyList<HumanAction>>([]));
        using var client = new HttpClient { BaseAddress = new Uri(Constants.Network.LoopbackUrl(Port)) };

        // Act
        var response = await client.PostAsync(Constants.Route.Authorize, new StringContent(""));
        var actual = await wait;

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(HumanGateDecision.AuthorizeAutomation, actual.Decision);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WaitForHumanAsync_Deny_ReturnsDenied()
    {
        // Arrange
        await using var host = new HandoffHost();
        await host.StartAsync(Constants.Network.TestOperatorPortAlt);
        var wait = host.WaitForHumanAsync(
            new InterventionRequest { RunId = "t", StepId = Constants.StepId.Confirm, Reason = "test" },
            () => Task.FromResult<IReadOnlyList<HumanAction>>(
                [new HumanAction { Kind = Constants.Action.Click, Detail = "unrelated" }]));
        using var client = new HttpClient { BaseAddress = new Uri(Constants.Network.LoopbackUrl(Constants.Network.TestOperatorPortAlt)) };

        // Act
        var response = await client.PostAsync(Constants.Route.Deny, new StringContent(""));
        var actual = await wait;

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(HumanGateDecision.Denied, actual.Decision);
        Assert.Single(actual.Actions);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WaitForHumanAsync_LegacyResumeRoute_ReturnsNotFound()
    {
        // Arrange
        await using var host = new HandoffHost();
        await host.StartAsync(Constants.Network.TestOperatorPortResume);
        var wait = host.WaitForHumanAsync(
            new InterventionRequest { RunId = "t", StepId = Constants.StepId.Confirm, Reason = "test" },
            () => Task.FromResult<IReadOnlyList<HumanAction>>(
                [new HumanAction { Kind = Constants.Action.Click, Detail = "unrelated" }]));
        using var client = new HttpClient { BaseAddress = new Uri(Constants.Network.LoopbackUrl(Constants.Network.TestOperatorPortResume)) };

        // Act
        var actual = await client.PostAsync(Constants.Route.Resume, new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);
        await client.PostAsync(Constants.Route.Deny, new StringContent(""));
        await wait;
    }
}
