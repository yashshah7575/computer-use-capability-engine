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
    public async Task DiscoverAsync_TypeParameterEqualsKnownMemberId_PersistsTemplate()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", parameter: Constants.Member.Known),
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
            Json("finish"),
            Json("finish"),
            Json("finish")));

        // Act
        var act = () => agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver(), AllowDiscovery(), evidence, maxSteps: 4);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("did not finish", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task DiscoverAsync_FinishWithoutBalanceExtract_Throws()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("checkpoint", textContains: "Account summary"),
            Json("finish"),
            Json("finish"),
            Json("finish")));

        // Act
        var act = () => agent.DiscoverAsync(
            Context(), new FakeSurfaceDriver { PageText = "Account summary" }, AllowDiscovery(), evidence, maxSteps: 5);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("did not finish", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_ExtractTextEqualToRuntimeValue_DropsVolatileLocator()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", css: "#acct", value: Constants.Member.Known, parameter: Constants.Field.MemberId),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", text: "1842.50", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));
        var surface = new FakeSurfaceDriver { PageText = "Account summary", ExtractText = "1842.50" };

        // Act
        var actual = await agent.DiscoverAsync(Context(), surface, AllowDiscovery(), evidence);

        // Assert
        var extract = actual.Steps.Single(s => s.Action == Constants.Action.Extract);
        Assert.DoesNotContain(extract.Locators, l => l.Value == "1842.50");
        Assert.Contains(extract.Locators, l => l.Strategy == Constants.Locator.Css && l.Value == "#savings");
        Assert.Contains(actual.Outputs, o => o.Name == Constants.Field.Balance);
    }

    [Fact]
    public void Build_Prompt_DoesNotLeakDemoBankSelector()
    {
        // Arrange
        const string observation = "CONTROLS\n[0]\ntag=input\nnameAttr=memberno";

        // Act
        var actual = DiscoveryPrompt.Build("look up a member", observation);

        // Assert
        Assert.DoesNotContain(Constants.Selector.MemberNumberInput, actual);
        Assert.Contains("<selector>", actual);
        Assert.Contains(observation, actual);
        Assert.Contains("do not type again", actual, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_TypeWithoutLocator_UsesNameAttrFromObservation()
    {
        // Arrange
        var evidence = NewEvidence();
        var agent = new DiscoveryAgent(new ScriptedLanguageModel(
            Json("type", parameter: Constants.Field.MemberId),
            Json("click", text: "Search"),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish")));
        var surface = new FakeSurfaceDriver
        {
            PageText = "Account summary",
            Observation = """
                CONTROLS
                [0]
                tag=input
                role=textbox
                label=Account
                nameAttr=acct
                """
        };

        // Act
        var actual = await agent.DiscoverAsync(Context(), surface, AllowDiscovery(), evidence);

        // Assert
        var typed = actual.Steps.Single(s => s.Action == Constants.Action.Type);
        Assert.Contains(typed.Locators, l => l.Strategy == Constants.Locator.Css && l.Value == "input[name=acct]");
    }

    [Fact]
    public async Task DiscoverAsync_RepeatTypeWhenFieldFilled_DoesNotRecordSecondType()
    {
        // Arrange
        var evidence = NewEvidence();
        var model = new ScriptedLanguageModel(
            Json("type", parameter: Constants.Field.MemberId),
            Json("type", parameter: Constants.Field.MemberId),
            Json("click", text: "Search"),
            Json("checkpoint", textContains: "Account summary"),
            Json("extract", css: "#savings", extractName: Constants.Field.Balance, outputType: Constants.Field.DecimalType),
            Json("finish"));
        var empty = """
            CONTROLS
            [0]
            tag=input
            role=textbox
            nameAttr=acct
            [1]
            tag=button
            role=button
            text=Search
            type=submit
            """;
        var filled = """
            CONTROLS
            [0]
            tag=input
            role=textbox
            nameAttr=acct
            text=12345
            [1]
            tag=button
            role=button
            text=Search
            type=submit
            """;
        var agent = new DiscoveryAgent(model);
        var surface = new FakeSurfaceDriver
        {
            PageText = "Account summary",
            Observation = empty,
            ObservationAfterType = filled
        };

        // Act
        var actual = await agent.DiscoverAsync(Context(), surface, AllowDiscovery(), evidence);

        // Assert
        Assert.Single(actual.Steps, s => s.Action == Constants.Action.Type);
        Assert.Contains(model.Prompts, p => p.Contains("already has a value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void For_Type_AddsCssFromNameAttr()
    {
        // Arrange
        var action = ModelActionParser.TryParse("""{"tool":"type","parameter":"memberId"}""")!;
        var observation = "CONTROLS\n[0]\ntag=input\nrole=textbox\nnameAttr=memberno\n";

        // Act
        var actual = LocatorEnricher.For(action, observation);

        // Assert
        Assert.Contains(actual, l => l.Strategy == Constants.Locator.Css && l.Value == "input[name=memberno]");
    }

    [Fact]
    public void For_ExtractBalance_AddsAdjacentCellCssFromObservation()
    {
        // Arrange
        var action = ModelActionParser.TryParse(
            """{"tool":"extract","extractName":"balance","outputType":"decimal"}""")!;
        var observation = "CONTROLS\n[0]\ntag=td\nrole=cell\nlabel=Savings\ntext=1842.50\n";

        // Act
        var actual = LocatorEnricher.For(action, observation);

        // Assert
        Assert.Contains(actual, l =>
            l.Strategy == Constants.Locator.Css && l.Value == "td:has-text(\"Savings\") + td");
        Assert.DoesNotContain(actual, l => l.Value == "1842.50");
    }

    [Fact]
    public void MaybeCoerce_ExtractOnSearchResults_BecomesClick()
    {
        // Arrange
        var action = ModelActionParser.TryParse(
            """{"tool":"extract","extractName":"balance","outputType":"decimal"}""")!;
        var observation = "CONTROLS\n[0]\ntag=a\nrole=link\ntext=Doe, Jane\nhref=/member?id=12345\n";

        // Act
        var actual = LocatorEnricher.MaybeCoerce(action, observation);

        // Assert
        Assert.Equal(Constants.Action.Click, actual.Tool);
    }

    [Fact]
    public void MaybeCoerce_CheckpointOnSearchResults_BecomesClick()
    {
        // Arrange
        var action = ModelActionParser.TryParse("""{"tool":"checkpoint","textContains":"Search results"}""")!;
        var observation = "CONTROLS\n[0]\ntag=a\nrole=link\ntext=Doe, Jane\nhref=/member?id=12345\n";

        // Act
        var actual = LocatorEnricher.MaybeCoerce(action, observation);

        // Assert
        Assert.Equal(Constants.Action.Click, actual.Tool);
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
