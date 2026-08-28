namespace ComputerUse.Domain;

/// <summary>
/// Compact, driver-neutral description of one visible interactive control.
/// </summary>
public sealed class ObservedControl
{
    public string Tag { get; set; } = "";
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public string? InputName { get; set; }
    public string? Type { get; set; }
    public string? Href { get; set; }
}
