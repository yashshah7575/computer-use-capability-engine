using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using ComputerUse.Domain;
using ComputerUse.Surfaces.Playwright;

namespace ComputerUse.Agent;

public sealed class DiscoveryAgent
{
    public async Task<CapabilityArtifact> DiscoverAsync(
        string goal,
        string baseUrl,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        int maxSteps = 16)
    {
        Directory.CreateDirectory(evidenceDir);
        await surface.NavigateAsync(baseUrl.TrimEnd('/') + "/");

        var model = Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID") ?? "amazon.nova-lite-v1:0";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var client = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(region));
        var outputs = new Dictionary<string, string>();

        for (var i = 0; i < maxSteps; i++)
        {
            var obs = await surface.ObserveAsync();
            await File.WriteAllTextAsync(Path.Combine(evidenceDir, $"obs-{i}.txt"), Redaction.Redact(obs));
            var prompt =
                "You operate a bank back-office UI. Goal: " + goal + "\nObservation:\n" + obs +
                "\nReply with ONE JSON object only, e.g. {\"tool\":\"click\",\"css\":\"...\",\"text\":\"...\"} or {\"tool\":\"finish\"}.";

            var resp = await client.ConverseAsync(new ConverseRequest
            {
                ModelId = model,
                Messages =
                [
                    new Message
                    {
                        Role = ConversationRole.User,
                        Content = [new ContentBlock { Text = prompt }]
                    }
                ]
            });

            var text = string.Join("", resp.Output.Message.Content.Select(c => c.Text)).Trim();
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                continue;
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;
            var tool = root.GetProperty("tool").GetString();
            var css = root.TryGetProperty("css", out var c) ? c.GetString() ?? "" : "";
            var t = root.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
            var loc = new List<LocatorSpec>();
            if (!string.IsNullOrEmpty(root.TryGetProperty("text", out var t2) ? t2.GetString() : null)
                && tool == "click")
                loc.Add(new LocatorSpec { Strategy = "text", Value = t });
            if (css.Length > 0)
                loc.Add(new LocatorSpec { Strategy = "css", Value = css });

            var denied = PolicyEngine.CheckAction(allowlist, tool == "finish" ? "extract" : tool ?? "click",
                new Uri(await surface.UrlAsync()));
            if (denied is not null && tool != "finish")
                throw new InvalidOperationException(denied.Message);

            switch (tool)
            {
                case "click":
                    await surface.ClickAsync(loc);
                    break;
                case "type":
                    await surface.TypeAsync(loc, t);
                    break;
                case "extract":
                    outputs["balance"] = await surface.ExtractAsync(loc);
                    break;
                case "finish":
                    return ScriptedLookup(baseUrl);
            }
        }

        if (outputs.Count > 0)
            return ScriptedLookup(baseUrl);
        throw new InvalidOperationException("Discovery did not finish.");
    }

    public static CapabilityArtifact ScriptedLookup(string _) => new()
    {
        SchemaVersion = "1.0.0",
        Id = "lookup-savings-balance",
        Description = "Look up a member by ID and extract current savings balance.",
        ArtifactVersion = 1,
        Inputs =
        [
            new() { Name = "memberId", Type = "string" },
            new() { Name = "baseUrl", Type = "string" }
        ],
        Outputs = [new() { Name = "balance", Type = "decimal" }],
        Steps =
        [
            new() { Id = "open-home", Action = "navigate", Url = "{{baseUrl}}/", Risk = "READ_ONLY" },
            new()
            {
                Id = "type-id", Action = "type", Value = "{{memberId}}", Risk = "REVERSIBLE",
                Locators = [new() { Strategy = "css", Value = "input[name=memberno]" }]
            },
            new()
            {
                Id = "submit", Action = "click", Risk = "REVERSIBLE",
                Locators =
                [
                    new() { Strategy = "role", Role = "button", Name = "Lookup" },
                    new() { Strategy = "text", Value = "Lookup" }
                ]
            },
            new()
            {
                Id = "open-member", Action = "click", Risk = "READ_ONLY",
                Locators = [new() { Strategy = "css", Value = "table a" }]
            },
            new()
            {
                Id = "checkpoint-member", Action = "checkpoint", TextContains = "Member record", Risk = "READ_ONLY"
            },
            new()
            {
                Id = "extract-balance", Action = "extract", ExtractName = "balance", Risk = "READ_ONLY",
                Locators = [new() { Strategy = "css", Value = "h2 + table tr:nth-child(2) td:nth-child(2)" }]
            }
        ]
    };

    public static CapabilityArtifact ScriptedSubAccount()
    {
        var a = ScriptedLookup("");
        a.Id = "open-sub-account";
        a.Description = "Start opening a sub-account (risky confirm).";
        a.Outputs = [];
        a.Steps.Add(new ArtifactStep
        {
            Id = "open-sub",
            Action = "click",
            Risk = "RISKY",
            Locators = [new() { Strategy = "text", Value = "Open sub-account" }]
        });
        a.Steps.Add(new ArtifactStep
        {
            Id = "confirm",
            Action = "click",
            Risk = "IRREVERSIBLE",
            Locators = [new() { Strategy = "text", Value = "Confirm open sub-account" }]
        });
        return a;
    }
}
