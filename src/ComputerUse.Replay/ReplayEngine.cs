using ComputerUse.Domain;
using ComputerUse.Surfaces.Playwright;

namespace ComputerUse.Replay;

public sealed class ReplayEngine
{
    public async Task<ExecutionResult> RunAsync(
        CapabilityArtifact artifact,
        IReadOnlyDictionary<string, string> inputs,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        bool simulateLocatorFailure = false,
        Func<ArtifactStep, Task<bool>>? onHumanGate = null)
    {
        Directory.CreateDirectory(evidenceDir);
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lastObs = "";
        var same = 0;

        foreach (var step in artifact.Steps)
        {
            try
            {
                var action = step.Action.ToLowerInvariant();
                Uri? uri = null;
                if (action == "navigate" && step.Url is not null)
                    uri = new Uri(ParameterSubstitution.Apply(step.Url, inputs));
                else
                {
                    try { uri = new Uri(await surface.UrlAsync()); } catch { /* ignore */ }
                }

                var denied = PolicyEngine.CheckAction(allowlist, action, uri);
                if (denied is not null)
                {
                    denied.StepId = step.Id;
                    return await FailAsync(surface, evidenceDir, denied);
                }

                if (PolicyEngine.RequiresHuman(PolicyEngine.ParseRisk(step.Risk)))
                {
                    if (onHumanGate is null || !await onHumanGate(step))
                    {
                        return new ExecutionResult
                        {
                            Kind = ResultKind.InterventionRequired,
                            StepId = step.Id,
                            Message = $"Step {step.Id} is {step.Risk} and requires a human.",
                            EvidenceDir = evidenceDir
                        };
                    }
                }

                if (simulateLocatorFailure && action is "click" or "type" or "extract")
                    throw new InvalidOperationException("Simulated locator failure.");

                switch (action)
                {
                    case "navigate":
                        await surface.NavigateAsync(ParameterSubstitution.Apply(step.Url!, inputs));
                        break;
                    case "type":
                        await surface.TypeAsync(step.Locators, ParameterSubstitution.Apply(step.Value ?? "", inputs));
                        break;
                    case "click":
                        await surface.ClickAsync(step.Locators);
                        break;
                    case "extract":
                        var text = await surface.ExtractAsync(step.Locators);
                        if (step.ExtractName is not null)
                            outputs[step.ExtractName] = text;
                        break;
                    case "checkpoint":
                        var ok = step.TextContains is null || await surface.PageContainsAsync(step.TextContains);
                        if (!ok)
                        {
                            var observed = await surface.ObserveAsync();
                            if (observed.Contains("not found", StringComparison.OrdinalIgnoreCase))
                            {
                                return new ExecutionResult
                                {
                                    Kind = ResultKind.BusinessOutcome,
                                    StepId = step.Id,
                                    Message = "Member not found.",
                                    Expected = step.TextContains,
                                    Observed = "Record not found",
                                    EvidenceDir = evidenceDir
                                };
                            }

                            return await FailAsync(surface, evidenceDir, new ExecutionResult
                            {
                                Kind = ResultKind.HardFailure,
                                StepId = step.Id,
                                Message = "Checkpoint failed.",
                                Expected = step.TextContains,
                                Observed = observed[..Math.Min(400, observed.Length)]
                            });
                        }
                        break;
                    case "wait":
                        await Task.Delay(400);
                        break;
                    default:
                        return new ExecutionResult
                        {
                            Kind = ResultKind.PolicyFailure,
                            StepId = step.Id,
                            Message = $"Unknown action {action}"
                        };
                }

                var obs = await surface.ObserveAsync();
                if (obs == lastObs) same++;
                else { lastObs = obs; same = 0; }
                if (same >= 5)
                {
                    return await FailAsync(surface, evidenceDir, new ExecutionResult
                    {
                        Kind = ResultKind.HardFailure,
                        StepId = step.Id,
                        Message = "No-progress loop detected."
                    });
                }
            }
            catch (Exception ex)
            {
                var observed = "";
                try { observed = await surface.ObserveAsync(); } catch { /* ignore */ }
                if (observed.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return new ExecutionResult
                    {
                        Kind = ResultKind.BusinessOutcome,
                        StepId = step.Id,
                        Message = "Member not found.",
                        Expected = "member record",
                        Observed = "Record not found",
                        EvidenceDir = evidenceDir
                    };
                }

                return await FailAsync(surface, evidenceDir, new ExecutionResult
                {
                    Kind = ResultKind.HardFailure,
                    StepId = step.Id,
                    Message = Redaction.Redact(ex.Message),
                    Expected = "control resolved",
                    Observed = ex.GetType().Name
                });
            }
        }

        var allowed = Redaction.AllowlistedOutputs(outputs, artifact.Outputs.Select(o => o.Name));
        return new ExecutionResult
        {
            Kind = ResultKind.Success,
            Message = "Replay succeeded.",
            Outputs = allowed,
            EvidenceDir = evidenceDir
        };
    }

    private static async Task<ExecutionResult> FailAsync(ISurfaceDriver surface, string dir, ExecutionResult r)
    {
        r.EvidenceDir = dir;
        try
        {
            await surface.ScreenshotAsync(Path.Combine(dir, "failure.png"));
            await File.WriteAllTextAsync(Path.Combine(dir, "snapshot.txt"), await surface.ObserveAsync());
        }
        catch { /* evidence best-effort */ }
        return r;
    }
}
