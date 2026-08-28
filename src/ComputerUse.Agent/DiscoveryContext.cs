using System.Text.RegularExpressions;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// Invocation context for a discovery run. Supported parameter names are fixed for this DemoBank slice.
/// </summary>
public sealed class DiscoveryContext
{
    public required string Goal { get; init; }
    public required string BaseUrl { get; init; }
    public IReadOnlyDictionary<string, string> KnownInputs { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> SupportedParameters =
        new(StringComparer.OrdinalIgnoreCase) { Constants.Field.MemberId, Constants.Field.BaseUrl };

    public static DiscoveryContext From(string goal, string baseUrl, string? memberId = null)
    {
        var trimmed = baseUrl.TrimEnd('/');
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.Field.BaseUrl] = trimmed
        };
        var id = memberId;
        if (string.IsNullOrWhiteSpace(id))
        {
            var match = Regex.Match(goal, @"\b(\d{5})\b");
            if (match.Success)
                id = match.Groups[1].Value;
        }

        if (!string.IsNullOrWhiteSpace(id))
            inputs[Constants.Field.MemberId] = id.Trim();

        return new DiscoveryContext
        {
            Goal = goal,
            BaseUrl = trimmed,
            KnownInputs = inputs
        };
    }
}
