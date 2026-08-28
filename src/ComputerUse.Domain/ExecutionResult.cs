namespace ComputerUse.Domain;

public sealed class ExecutionResult
{
    public ResultKind Kind { get; set; }
    public string Message { get; set; } = "";
    public string? StepId { get; set; }
    public string? Expected { get; set; }
    public string? Observed { get; set; }
    public Dictionary<string, string> Outputs { get; set; } = [];
    public List<Degradation> Degradations { get; set; } = [];
    public List<RecoveryEvent> RecoveryEvents { get; set; } = [];
    public List<HumanAction> HumanActions { get; set; } = [];
    public string? EvidenceDir { get; set; }
}
