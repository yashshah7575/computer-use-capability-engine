namespace ComputerUse.Domain;

public sealed class RecoveryEvent
{
    public string Code { get; set; } = "";
    public string StepId { get; set; } = "";
    public string Action { get; set; } = "";
}
