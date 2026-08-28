using ComputerUse.Domain;

namespace ComputerUse.Replay;

/// <summary>
/// Deterministic execution of a saved capability artifact. No language model in this loop.
/// </summary>
public interface IReplayEngine
{
    /// <summary>Replays <paramref name="request"/> against the supplied surface and policy.</summary>
    /// <param name="request">Artifact, inputs, surface, allowlist, and run options.</param>
    /// <returns>Structured result (success, business outcome, recoverable, or failure).</returns>
    Task<ExecutionResult> RunAsync(ReplayRequest request);
}
