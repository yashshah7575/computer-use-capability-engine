namespace ComputerUse.Domain;

public sealed class ArtifactStep
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Url { get; set; }
    public string? Value { get; set; }
    public string? ExtractName { get; set; }
    public string? TextContains { get; set; }
    public string Risk { get; set; } = Constants.Risk.ReadOnly;
    public List<LocatorSpec> Locators { get; set; } = [];
}
