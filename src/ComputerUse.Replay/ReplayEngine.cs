using ComputerUse.Domain;

namespace ComputerUse.Replay;

public sealed class ReplayEngine : IReplayEngine
{
    public Task<ExecutionResult> RunAsync(
        CapabilityArtifact artifact,
        IReadOnlyDictionary<string, string> inputs,
        ISurfaceDriver surface,
        AllowlistConfig allowlist,
        string evidenceDir,
        bool simulateLocatorFailure = false,
        Func<ArtifactStep, Task<HumanGateOutcome>>? onHumanGate = null,
        bool allowDraft = false) =>
        RunAsync(new ReplayRequest
        {
            Artifact = artifact,
            Inputs = inputs,
            Surface = surface,
            Allowlist = allowlist,
            EvidenceDir = evidenceDir,
            SimulateLocatorFailure = simulateLocatorFailure,
            OnHumanGate = onHumanGate,
            AllowDraft = allowDraft
        });

    public async Task<ExecutionResult> RunAsync(ReplayRequest request)
    {
        var artifact = request.Artifact;
        var inputs = request.Inputs;
        var surface = request.Surface;
        var allowlist = request.Allowlist;
        var evidenceDir = request.EvidenceDir;
        var simulateLocatorFailure = request.SimulateLocatorFailure;
        var onHumanGate = request.OnHumanGate;
        var allowDraft = request.AllowDraft;

        Directory.CreateDirectory(evidenceDir);
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var degradations = new List<Degradation>();
        var recoveryEvents = new List<RecoveryEvent>();
        var humanActions = new List<HumanAction>();
        var recoveryUses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastObs = "";
        var same = 0;

        ExecutionResult Attach(ExecutionResult r)
        {
            r.Degradations = degradations;
            r.RecoveryEvents = recoveryEvents;
            r.HumanActions = humanActions;
            r.EvidenceDir = evidenceDir;
            return r;
        }

        var approval = PolicyEngine.CheckApproval(artifact, allowDraft);
        if (approval is not null)
            return Attach(approval);

        foreach (var step in artifact.Steps)
        {
            var attempts = 0;
            while (true)
            {
                attempts++;
                try
                {
                    var action = step.Action.ToLowerInvariant();
                    Uri? uri = null;
                    if (action == Constants.Action.Navigate && step.Url is not null)
                        uri = new Uri(ParameterSubstitution.Apply(step.Url, inputs));
                    else
                    {
                        try { uri = new Uri(await surface.UrlAsync()); } catch { /* ignore */ }
                    }

                    var denied = PolicyEngine.CheckAction(allowlist, action, uri);
                    if (denied is not null)
                    {
                        denied.StepId = step.Id;
                        return await FailAsync(surface, Attach(denied));
                    }

                    if (PolicyEngine.RequiresHuman(PolicyEngine.ParseRisk(step.Risk)))
                    {
                        if (onHumanGate is null)
                        {
                            return Attach(new ExecutionResult
                            {
                                Kind = ResultKind.InterventionRequired,
                                StepId = step.Id,
                                Message = $"Step {step.Id} is {step.Risk} and requires a human."
                            });
                        }

                        var gate = await onHumanGate(step);
                        humanActions.AddRange(gate.Actions);
                        if (gate.Decision == HumanGateDecision.Denied)
                        {
                            return Attach(new ExecutionResult
                            {
                                Kind = ResultKind.InterventionRequired,
                                StepId = step.Id,
                                Message = $"Step {step.Id} denied by human."
                            });
                        }

                        if (gate.Decision == HumanGateDecision.CompletedByHuman)
                        {
                            if (!await HumanCompletionVerifiedAsync(artifact, step, surface))
                            {
                                return Attach(new ExecutionResult
                                {
                                    Kind = ResultKind.InterventionRequired,
                                    StepId = step.Id,
                                    Message = $"Step {step.Id} marked completed by human but completion could not be verified."
                                });
                            }

                            break;
                        }

                        if (gate.Decision != HumanGateDecision.AuthorizeAutomation)
                        {
                            return Attach(new ExecutionResult
                            {
                                Kind = ResultKind.InterventionRequired,
                                StepId = step.Id,
                                Message = $"Step {step.Id} has no explicit human authorization."
                            });
                        }
                    }

                    await MaybeRecoverAsync(artifact, surface, step, recoveryEvents, recoveryUses);

                    if (simulateLocatorFailure && action is Constants.Action.Click or Constants.Action.Type or Constants.Action.Extract)
                        throw new InvalidOperationException("Simulated locator failure.");

                    switch (action)
                    {
                        case Constants.Action.Navigate:
                            await surface.NavigateAsync(ParameterSubstitution.Apply(step.Url!, inputs));
                            break;
                        case Constants.Action.Type:
                            RecordMatch(degradations, step.Id, await surface.TypeAsync(
                                step.Locators, ParameterSubstitution.Apply(step.Value ?? "", inputs)));
                            break;
                        case Constants.Action.Click:
                            RecordMatch(degradations, step.Id, await surface.ClickAsync(step.Locators));
                            break;
                        case Constants.Action.Extract:
                            var extracted = await surface.ExtractAsync(step.Locators);
                            RecordMatch(degradations, step.Id, extracted.Match);
                            if (step.ExtractName is not null)
                                outputs[step.ExtractName] = extracted.Text;
                            break;
                        case Constants.Action.Checkpoint:
                            var ok = step.TextContains is null || await surface.PageContainsAsync(step.TextContains);
                            if (!ok)
                            {
                                var observed = await surface.ObserveAsync();
                                var known = await MatchKnownOutcomeAsync(artifact, surface);
                                if (known is not null)
                                {
                                    return Attach(new ExecutionResult
                                    {
                                        Kind = ResultKind.BusinessOutcome,
                                        StepId = step.Id,
                                        Message = known.Code,
                                        Expected = step.TextContains,
                                        Observed = known.TextContains ?? known.Code
                                    });
                                }

                                if (attempts < Constants.Timing.MaxStepAttempts && await MaybeRecoverAsync(artifact, surface, step, recoveryEvents, recoveryUses))
                                    continue;

                                return await FailAsync(surface, Attach(new ExecutionResult
                                {
                                    Kind = ResultKind.HardFailure,
                                    StepId = step.Id,
                                    Message = "Checkpoint failed.",
                                    Expected = step.TextContains,
                                    Observed = observed[..Math.Min(Constants.Timing.ObservedSnippetLength, observed.Length)]
                                }));
                            }
                            break;
                        case Constants.Action.Wait:
                            await Task.Delay(Constants.Timing.WaitStepMilliseconds);
                            break;
                        default:
                            return Attach(new ExecutionResult
                            {
                                Kind = ResultKind.PolicyFailure,
                                StepId = step.Id,
                                Message = $"Unknown action {action}"
                            });
                    }

                    var obs = await surface.ObserveAsync();
                    if (obs == lastObs) same++;
                    else { lastObs = obs; same = 0; }
                    if (same >= Constants.Timing.NoProgressLimit)
                    {
                        return await FailAsync(surface, Attach(new ExecutionResult
                        {
                            Kind = ResultKind.HardFailure,
                            StepId = step.Id,
                            Message = "No-progress loop detected."
                        }));
                    }

                    break;
                }
                catch (Exception ex)
                {
                    var known = await MatchKnownOutcomeAsync(artifact, surface);
                    if (known is not null)
                    {
                        return Attach(new ExecutionResult
                        {
                            Kind = ResultKind.BusinessOutcome,
                            StepId = step.Id,
                            Message = known.Code,
                            Expected = Constants.Outcome.ControlResolved,
                            Observed = known.TextContains ?? known.Code
                        });
                    }

                    if (attempts < Constants.Timing.MaxStepAttempts && await MaybeRecoverAsync(artifact, surface, step, recoveryEvents, recoveryUses))
                        continue;

                    return await FailAsync(surface, Attach(new ExecutionResult
                    {
                        Kind = ResultKind.HardFailure,
                        StepId = step.Id,
                        Message = Redaction.Redact(ex.Message),
                        Expected = Constants.Outcome.ControlResolved,
                        Observed = ex.GetType().Name
                    }));
                }
            }
        }

        var allowed = Redaction.AllowlistedOutputs(outputs, artifact.Outputs.Select(o => o.Name));
        return Attach(new ExecutionResult
        {
            Kind = recoveryEvents.Count > 0 ? ResultKind.Recoverable : ResultKind.Success,
            Message = recoveryEvents.Count > 0 ? "Replay succeeded after recovery." : "Replay succeeded.",
            Outputs = allowed
        });
    }

    public static StabilityReport Summarize(IReadOnlyList<ExecutionResult> results) =>
        StabilityReport.From(results);

    private static async Task<bool> HumanCompletionVerifiedAsync(
        CapabilityArtifact artifact,
        ArtifactStep step,
        ISurfaceDriver surface)
    {
        if (step.Locators.Count > 0 && !await surface.CanResolveAsync(step.Locators))
            return true;

        var seen = false;
        foreach (var candidate in artifact.Steps)
        {
            if (!seen)
            {
                if (ReferenceEquals(candidate, step))
                    seen = true;
                continue;
            }

            if (candidate.Action == Constants.Action.Checkpoint &&
                !string.IsNullOrEmpty(candidate.TextContains) &&
                await surface.PageContainsAsync(candidate.TextContains))
                return true;
            break;
        }

        return false;
    }

    private static void RecordMatch(List<Degradation> degradations, string stepId, LocatorMatch match)
    {
        if (match.MatchedIndex <= 0)
            return;
        degradations.Add(new Degradation
        {
            StepId = stepId,
            Kind = Constants.DegradationKind.TierDegraded,
            MatchedLocatorIndex = match.MatchedIndex,
            Message = $"Locator index {match.MatchedIndex} won."
        });
    }

    private static async Task<KnownOutcome?> MatchKnownOutcomeAsync(CapabilityArtifact artifact, ISurfaceDriver surface)
    {
        string url;
        try { url = await surface.UrlAsync(); }
        catch { url = ""; }

        foreach (var outcome in artifact.KnownOutcomes)
        {
            if (!string.IsNullOrEmpty(outcome.TextContains) &&
                !await surface.PageContainsAsync(outcome.TextContains))
                continue;
            if (!string.IsNullOrEmpty(outcome.UrlContains) &&
                url.IndexOf(outcome.UrlContains, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            return outcome;
        }

        return null;
    }

    private static async Task<bool> MaybeRecoverAsync(
        CapabilityArtifact artifact,
        ISurfaceDriver surface,
        ArtifactStep step,
        List<RecoveryEvent> events,
        Dictionary<string, int> uses)
    {
        var recovered = false;
        foreach (var condition in artifact.RecoverableConditions)
        {
            if (!await surface.PageContainsAsync(condition.TextContains))
                continue;
            var used = uses.GetValueOrDefault(condition.Code);
            if (used >= condition.MaxRetries)
                continue;

            uses[condition.Code] = used + 1;
            if (condition.Action == Constants.Recovery.Dismiss && condition.Locators.Count > 0)
                await surface.ClickAsync(condition.Locators);
            else
                await Task.Delay(Constants.Timing.WaitStepMilliseconds);

            events.Add(new RecoveryEvent
            {
                Code = condition.Code,
                StepId = step.Id,
                Action = condition.Action
            });
            recovered = true;
        }

        return recovered;
    }

    private static async Task<ExecutionResult> FailAsync(ISurfaceDriver surface, ExecutionResult r)
    {
        try
        {
            var dir = r.EvidenceDir ?? ".";
            await surface.ScreenshotAsync(Path.Combine(dir, Constants.PathName.FailureScreenshot));
            await File.WriteAllTextAsync(Path.Combine(dir, Constants.PathName.Snapshot), await surface.ObserveAsync());
        }
        catch { /* evidence best-effort */ }
        return r;
    }
}
