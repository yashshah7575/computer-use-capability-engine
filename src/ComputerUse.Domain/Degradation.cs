namespace ComputerUse.Domain;

public sealed class Degradation
{
    public string StepId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int? MatchedLocatorIndex { get; set; }
    public string Message { get; set; } = "";
}
