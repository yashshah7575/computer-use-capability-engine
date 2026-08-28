namespace ComputerUse.Domain;

public enum RiskClass
{
    ReadOnly,
    Reversible,
    Risky,
    Irreversible
}

public enum ResultKind
{
    Success,
    BusinessOutcome,
    Recoverable,
    HardFailure,
    PolicyFailure,
    InterventionRequired
}

public enum ControllerKind
{
    Automation,
    Human
}

public sealed class LocatorSpec
{
    public string Strategy { get; set; } = "css";
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
}

public sealed class ArtifactStep
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Url { get; set; }
    public string? Value { get; set; }
    public string? ExtractName { get; set; }
    public string? TextContains { get; set; }
    public string Risk { get; set; } = "READ_ONLY";
    public List<LocatorSpec> Locators { get; set; } = [];
}

public sealed class TypedField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
}

public sealed class CapabilityArtifact
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public int ArtifactVersion { get; set; } = 1;
    public List<TypedField> Inputs { get; set; } = [];
    public List<TypedField> Outputs { get; set; } = [];
    public List<ArtifactStep> Steps { get; set; } = [];
}

public sealed class ExecutionResult
{
    public ResultKind Kind { get; set; }
    public string Message { get; set; } = "";
    public string? StepId { get; set; }
    public string? Expected { get; set; }
    public string? Observed { get; set; }
    public Dictionary<string, string> Outputs { get; set; } = [];
    public string? EvidenceDir { get; set; }
}

public sealed class AllowlistConfig
{
    public List<string> AllowedHosts { get; set; } = [];
    public List<int> AllowedPorts { get; set; } = [];
    public List<string> AllowedPathPrefixes { get; set; } = [];
    public List<string> AllowedActions { get; set; } = [];
    public List<string> ProhibitedPathPrefixes { get; set; } = [];
}

public sealed class InterventionRequest
{
    public string RunId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string StepId { get; set; } = "";
    public string ScreenshotPath { get; set; } = "";
    public ControllerKind Controller { get; set; } = ControllerKind.Human;
}
