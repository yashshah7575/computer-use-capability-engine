namespace ComputerUse.Domain;

public sealed class HumanGateOutcome
{
    public HumanGateDecision Decision { get; set; } = HumanGateDecision.Denied;
    public List<HumanAction> Actions { get; set; } = [];
}
