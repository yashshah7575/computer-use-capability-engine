namespace ComputerUse.Domain;

public sealed class RecoverableCondition
{
    public string Code { get; set; } = "";
    public string TextContains { get; set; } = "";
    public string Action { get; set; } = Constants.Recovery.Dismiss;
    public List<LocatorSpec> Locators { get; set; } = [];
    public int MaxRetries { get; set; } = 1;
}
