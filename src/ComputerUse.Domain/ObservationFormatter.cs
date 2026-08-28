using System.Text;

namespace ComputerUse.Domain;

/// <summary>
/// Formats a compact PAGE / VISIBLE TEXT / CONTROLS observation. No DOM dump.
/// </summary>
public static class ObservationFormatter
{
    public const int MaxVisibleTextChars = 800;
    public const int MaxControls = 24;

    public static string Format(string url, string title, string visibleText, IReadOnlyList<ObservedControl> controls)
    {
        var text = (visibleText ?? "").Trim();
        if (text.Length > MaxVisibleTextChars)
            text = text[..MaxVisibleTextChars] + "…";

        var sb = new StringBuilder();
        sb.AppendLine("PAGE");
        sb.Append("URL: ").AppendLine(url);
        sb.Append("TITLE: ").AppendLine(title);
        sb.AppendLine();
        sb.AppendLine("VISIBLE TEXT");
        sb.AppendLine(text);
        sb.AppendLine();
        sb.AppendLine("CONTROLS");
        var i = 0;
        foreach (var c in controls.Take(MaxControls))
        {
            sb.Append('[').Append(i++).AppendLine("]");
            Append(sb, "tag", c.Tag);
            Append(sb, "role", c.Role);
            Append(sb, "name", c.Name);
            Append(sb, "text", c.Text);
            Append(sb, "label", c.Label);
            Append(sb, "placeholder", c.Placeholder);
            Append(sb, "nameAttr", c.InputName);
            Append(sb, "type", c.Type);
            Append(sb, "href", c.Href);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static void Append(StringBuilder sb, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        sb.Append(key).Append('=').AppendLine(value.Trim());
    }
}
