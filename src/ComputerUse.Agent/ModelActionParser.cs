using System.Text.Json;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

internal sealed class ModelAction
{
    public string Tool { get; init; } = "";
    public string? Css { get; init; }
    public string? Text { get; init; }
    public string? Value { get; init; }
    public string? Parameter { get; init; }
    public string? ExtractName { get; init; }
    public string? OutputType { get; init; }
    public string? TextContains { get; init; }
    public string? Role { get; init; }
    public string? Name { get; init; }
    public string? Placeholder { get; init; }
    public string? Label { get; init; }
    public string? Url { get; init; }
}

internal static class ModelActionParser
{
    public static ModelAction? TryParse(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var root = doc.RootElement;
            var tool = Str(root, Constants.Llm.Tool);
            if (string.IsNullOrWhiteSpace(tool))
                return null;
            return new ModelAction
            {
                Tool = tool.Trim().ToLowerInvariant(),
                Css = Str(root, Constants.Llm.Css),
                Text = Str(root, Constants.Llm.Text),
                Value = Str(root, Constants.Llm.Value),
                Parameter = Str(root, Constants.Llm.Parameter),
                ExtractName = Str(root, Constants.Llm.ExtractName),
                OutputType = Str(root, Constants.Llm.OutputType),
                TextContains = Str(root, Constants.Llm.TextContains),
                Role = Str(root, Constants.Llm.Role),
                Name = Str(root, Constants.Llm.Name),
                Placeholder = Str(root, Constants.Llm.Placeholder),
                Label = Str(root, Constants.Llm.Label),
                Url = Str(root, Constants.Llm.Url)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static List<LocatorSpec> Locators(ModelAction action)
    {
        var list = new List<LocatorSpec>();
        var roleName = FirstNonEmpty(action.Name, action.Label, action.Text);
        if (!string.IsNullOrWhiteSpace(action.Role) && !string.IsNullOrWhiteSpace(roleName))
            list.Add(new LocatorSpec { Strategy = Constants.Locator.Role, Role = action.Role, Name = roleName });
        if (!string.IsNullOrWhiteSpace(action.Label))
            list.Add(new LocatorSpec { Strategy = Constants.Locator.Label, Value = action.Label });
        if (!string.IsNullOrWhiteSpace(action.Placeholder))
            list.Add(new LocatorSpec { Strategy = Constants.Locator.Placeholder, Value = action.Placeholder });
        if (!string.IsNullOrWhiteSpace(action.Text))
            list.Add(new LocatorSpec { Strategy = Constants.Locator.Text, Value = action.Text });
        if (!string.IsNullOrWhiteSpace(action.Css))
            list.Add(new LocatorSpec { Strategy = Constants.Locator.Css, Value = action.Css });
        return list;
    }

    /// <summary>
    /// Drops extract locators that equal the runtime extracted value so the artifact stays reusable.
    /// </summary>
    public static List<LocatorSpec> StableExtractLocators(IReadOnlyList<LocatorSpec> locators, string extracted)
    {
        var value = extracted.Trim();
        if (string.IsNullOrEmpty(value))
            return locators.ToList();
        return locators
            .Where(l =>
                !value.Equals(l.Value?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !value.Equals(l.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) ? el.GetString() : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
