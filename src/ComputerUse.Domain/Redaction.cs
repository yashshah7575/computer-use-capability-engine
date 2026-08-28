using System.Text.RegularExpressions;

namespace ComputerUse.Domain;

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
