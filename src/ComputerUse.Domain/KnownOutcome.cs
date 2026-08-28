namespace ComputerUse.Domain;

public sealed class KnownOutcome
{
    public string Code { get; set; } = "";
    public string? TextContains { get; set; }
    public string? UrlContains { get; set; }
}
