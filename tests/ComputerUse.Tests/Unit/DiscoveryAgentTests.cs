using ComputerUse.Agent;
using ComputerUse.Domain;
using ComputerUse.Tests.Fakes;
using Xunit;

namespace ComputerUse.Tests.Unit;

public class DiscoveryAgentTests
{
    [Fact]
    public async Task DiscoverAsync_RecordedSequence_DiffersFromScriptedLookup()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("click", text: "Search"),
            Json("click", text: "Open record"),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));
        var surface = new FakeSurfaceDriver { PageText = "Account summary" };

        // Act
        var actual = await agent.DiscoverAsync(Context(), surface, AllowDiscovery(), evidence);

        // Assert
        var fixture = DiscoveryAgent.ScriptedLookup(Constants.Network.DemoBankUrl);
        Assert.NotEqual(fixture.Steps.Select(s => s.Id), actual.Steps.Select(s => s.Id));
        Assert.Contains(actual.Steps, s => s.Action == Constants.Action.Checkpoint && s.TextContains == "Account summary");
        Assert.DoesNotContain(actual.Steps, s => s.TextContains == Constants.Ui.MemberRecord);
        Assert.Contains(actual.Steps, s => s.Action == Constants.Action.Click && s.Locators.Any(l => l.Value == "Search"));
        Assert.Contains(actual.Steps, s => s.Action == Constants.Action.Click && s.Locators.Any(l => l.Value == "Open record"));
        Assert.Equal(Constants.Approval.Draft, actual.ApprovalState);
        Assert.True(File.Exists(Path.Combine(evidence, Constants.PathName.DiscoveryLog)));
    }

    [Fact]
    public async Task DiscoverAsync_TypeWithMemberIdParameter_PersistsTemplateNotLiteral()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("click", text: "Search"),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));

        // Act
        var actual = await agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver { PageText = "Account summary" }, AllowDiscovery(), evidence);

        // Assert
        var typed = actual.Steps.Single(s => s.Action == Constants.Action.Type);
        Assert.Equal(Constants.Template.MemberId, typed.Value);
        Assert.DoesNotContain(Constants.Member.Known, typed.Value);
        Assert.Contains(actual.Inputs, i => i.Name == Constants.Field.MemberId);
    }

    [Fact]
    public async Task DiscoverAsync_ExtractBalanceDecimal_DeclaresTypedOutput()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));

        // Act
        var actual = await agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver { PageText = "Account summary" }, AllowDiscovery(), evidence);

        // Assert
        var output = Assert.Single(actual.Outputs);
        Assert.Equal(Constants.Field.Balance, output.Name);
        Assert.Equal(Constants.Field.DecimalType, output.Type);
        Assert.Contains(actual.Steps, s => s.Action == Constants.Action.Extract && s.ExtractName == Constants.Field.Balance);
    }

    [Fact]
    public async Task DiscoverAsync_FinishWithoutCheckpoint_ThrowsAndDoesNotReturnScriptedLookup()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("finish")));

        // Act
        var act = () => agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver(), AllowDiscovery(), evidence);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("checkpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_SuccessfulRun_DefaultsToDraft()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));

        // Act
        var actual = await agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver { PageText = "Account summary" }, AllowDiscovery(), evidence);

        // Assert
        Assert.Equal(Constants.Approval.Draft, actual.ApprovalState);
        Assert.NotEqual(DiscoveryAgent.ScriptedLookup("").ApprovalState, actual.ApprovalState);
    }

    private static DiscoveryContext Context() =>
        DiscoveryContext.From(
            $"look up member {Constants.Member.Known}",
            Constants.Network.DemoBankUrl,
            Constants.Member.Known);

    private static AllowlistConfig AllowDiscovery() => new()
    {
        AllowedHosts = [Constants.Network.Loopback],
        AllowedPorts = [Constants.Network.DemoBankPort],
        AllowedPathPrefixes = ["/"],
        AllowedActions = [.. Constants.Action.ReplayAllowlist]
    };

    private static string NewEvidence() =>
        Path.Combine(Path.GetTempPath(), "cu-disc-" + Guid.NewGuid().ToString("N"));

    private static string Json(
        string tool,
        string? css = null,
        string? text = null,
        string? value = null,
        string? parameter = null,
        string? extractName = null,
        string? outputType = null,
        string? textContains = null)
    {
        var parts = new List<string> { $"\"tool\":\"{tool}\"" };
        if (css is not null) parts.Add($"\"css\":\"{css}\"");
        if (text is not null) parts.Add($"\"text\":\"{text}\"");
        if (value is not null) parts.Add($"\"value\":\"{value}\"");
        if (parameter is not null) parts.Add($"\"parameter\":\"{parameter}\"");
        if (extractName is not null) parts.Add($"\"extractName\":\"{extractName}\"");
        if (outputType is not null) parts.Add($"\"outputType\":\"{outputType}\"");
        if (textContains is not null) parts.Add($"\"textContains\":\"{textContains}\"");
        return "{" + string.Join(",", parts) + "}";
    }
}
