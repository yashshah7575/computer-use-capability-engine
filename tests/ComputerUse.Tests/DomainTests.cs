using ComputerUse.Agent;
using ComputerUse.Domain;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace ComputerUse.Tests;

public class DomainTests
{
    [Fact]
    public void Artifact_round_trip()
    {
        var a = DiscoveryAgent.ScriptedLookup("http://127.0.0.1:5100");
        var json = ArtifactSerializer.Serialize(a);
        var b = ArtifactSerializer.Deserialize(json);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.Steps.Count, b.Steps.Count);
    }

    [Fact]
    public void Parameter_substitution()
    {
        var s = ParameterSubstitution.Apply("{{baseUrl}}/x/{{memberId}}",
            new Dictionary<string, string> { ["baseUrl"] = "http://127.0.0.1:5100", ["memberId"] = "12345" });
        Assert.Equal("http://127.0.0.1:5100/x/12345", s);
    }

    [Fact]
    public void Redaction_denies_secrets()
    {
        Assert.Equal("[REDACTED]", Redaction.Redact("password=hunter2"));
        Assert.Equal("ok", Redaction.Redact("ok"));
    }

    [Fact]
    public void Allowlist_blocks_host()
    {
        var cfg = new AllowlistConfig
        {
            AllowedHosts = ["127.0.0.1"],
            AllowedPorts = [5100],
            AllowedPathPrefixes = ["/"],
            AllowedActions = ["navigate"]
        };
        var r = PolicyEngine.CheckAction(cfg, "navigate", new Uri("https://evil.example/"));
        Assert.Equal(ResultKind.PolicyFailure, r!.Kind);
    }

    [Fact]
    public void Risky_requires_human()
    {
        Assert.True(PolicyEngine.RequiresHuman(RiskClass.Risky));
        Assert.False(PolicyEngine.RequiresHuman(RiskClass.ReadOnly));
    }

    [Property]
    public void Json_round_trip_preserves_id(NonEmptyString id)
    {
        var a = DiscoveryAgent.ScriptedLookup("");
        a.Id = id.Get.Replace("\"", "");
        if (string.IsNullOrWhiteSpace(a.Id)) return;
        var b = ArtifactSerializer.Deserialize(ArtifactSerializer.Serialize(a));
        Assert.Equal(a.Id, b.Id);
    }
}
