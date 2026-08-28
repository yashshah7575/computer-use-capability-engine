using System.Text.Json;
using System.Text.RegularExpressions;

namespace ComputerUse.Domain;

public static class ArtifactSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string Serialize(CapabilityArtifact artifact) =>
        JsonSerializer.Serialize(artifact, JsonOptions);

    public static CapabilityArtifact Deserialize(string json)
    {
        var artifact = JsonSerializer.Deserialize<CapabilityArtifact>(json, JsonOptions)
            ?? throw new InvalidOperationException("Artifact JSON deserialized to null.");
        Validate(artifact);
        return artifact;
    }

    public static void Validate(CapabilityArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.SchemaVersion))
            throw new InvalidOperationException("schemaVersion is required.");
        if (string.IsNullOrWhiteSpace(artifact.Id))
            throw new InvalidOperationException("id is required.");
        if (artifact.Steps.Count == 0)
            throw new InvalidOperationException("steps must not be empty.");
        foreach (var step in artifact.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || string.IsNullOrWhiteSpace(step.Action))
                throw new InvalidOperationException("Each step needs id and action.");
        }
    }
}

public static class ParameterSubstitution
{
    public static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        return Regex.Replace(template, "\\{\\{([^}]+)\\}\\}", m =>
        {
            var key = m.Groups[1].Value.Trim();
            if (!values.TryGetValue(key, out var v))
                throw new InvalidOperationException($"Missing parameter '{key}'.");
            return v;
        });
    }
}

public static class Redaction
{
    private static readonly Regex Deny = new(
        "password|secret|token|ssn|account_number|aws_secret|authorization",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Redact(string text)
    {
        if (Deny.IsMatch(text))
            return "[REDACTED]";
        return text;
    }

    public static Dictionary<string, string> AllowlistedOutputs(
        IReadOnlyDictionary<string, string> raw,
        IEnumerable<string> allowedNames)
    {
        var set = allowedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return raw.Where(kv => set.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }
}

public static class PolicyEngine
{
    public static RiskClass ParseRisk(string risk) => risk.ToUpperInvariant() switch
    {
        "READ_ONLY" => RiskClass.ReadOnly,
        "REVERSIBLE" => RiskClass.Reversible,
        "RISKY" => RiskClass.Risky,
        "IRREVERSIBLE" => RiskClass.Irreversible,
        _ => RiskClass.Risky
    };

    public static bool RequiresHuman(RiskClass risk) =>
        risk is RiskClass.Risky or RiskClass.Irreversible;

    public static ExecutionResult? CheckAction(AllowlistConfig cfg, string action, Uri? uri)
    {
        if (!cfg.AllowedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Action '{action}' is not allowlisted."
            };
        }

        if (uri is null)
            return null;

        if (!cfg.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Host '{uri.Host}' is not allowlisted."
            };
        }

        if (cfg.AllowedPorts.Count > 0 && !cfg.AllowedPorts.Contains(uri.Port))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Port '{uri.Port}' is not allowlisted."
            };
        }

        foreach (var p in cfg.ProhibitedPathPrefixes)
        {
            if (uri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                return new ExecutionResult
                {
                    Kind = ResultKind.PolicyFailure,
                    Message = $"Path '{uri.AbsolutePath}' is prohibited."
                };
            }
        }

        if (cfg.AllowedPathPrefixes.Count > 0 &&
            !cfg.AllowedPathPrefixes.Any(p => uri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Path '{uri.AbsolutePath}' is not allowlisted."
            };
        }

        return null;
    }
}
