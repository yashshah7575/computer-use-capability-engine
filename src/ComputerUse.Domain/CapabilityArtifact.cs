namespace ComputerUse.Domain;

public sealed class CapabilityArtifact
{
    public string SchemaVersion { get; set; } = Constants.Schema.Version;
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public int ArtifactVersion { get; set; } = 1;
    public string ApprovalState { get; set; } = Constants.Approval.Approved;
    public List<TypedField> Inputs { get; set; } = [];
    public List<TypedField> Outputs { get; set; } = [];
    public List<KnownOutcome> KnownOutcomes { get; set; } = [];
    public List<RecoverableCondition> RecoverableConditions { get; set; } = [];
    public List<ArtifactStep> Steps { get; set; } = [];
}
