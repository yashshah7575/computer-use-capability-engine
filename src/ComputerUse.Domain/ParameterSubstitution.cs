using System.Text.RegularExpressions;

namespace ComputerUse.Domain;

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
