namespace ComputerUse.Domain;

public sealed class ExtractResult
{
    public string Text { get; set; } = "";
    public LocatorMatch Match { get; set; } = new();
}
