using ComputerUse.Domain;

namespace ComputerUse.Replay;

/// <summary>
/// Named options for a single replay invocation (avoids a long boolean parameter list).
/// </summary>
public sealed class ReplayRequest
{
    public required CapabilityArtifact Artifact { get; init; }
    public required IReadOnlyDictionary<string, string> Inputs { get; init; }
    public required ISurfaceDriver Surface { get; init; }
    public required AllowlistConfig Allowlist { get; init; }
    public required string EvidenceDir { get; init; }
    public bool SimulateLocatorFailure { get; init; }
    public Func<ArtifactStep, Task<HumanGateOutcome>>? OnHumanGate { get; init; }
    public bool AllowDraft { get; init; }
}
