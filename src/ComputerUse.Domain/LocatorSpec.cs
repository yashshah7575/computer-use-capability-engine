namespace ComputerUse.Domain;

public sealed class LocatorSpec
{
    public string Strategy { get; set; } = Constants.Locator.Css;
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
}
