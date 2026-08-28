namespace ComputerUse.Domain;

public sealed class HumanGateOutcome
{
    public bool Granted { get; set; }
    public List<HumanAction> Actions { get; set; } = [];
}
