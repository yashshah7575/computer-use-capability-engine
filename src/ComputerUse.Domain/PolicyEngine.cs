namespace ComputerUse.Domain;

public static class PolicyEngine
{
    public static RiskClass ParseRisk(string risk) => risk.ToUpperInvariant() switch
    {
        Constants.Risk.ReadOnly => RiskClass.ReadOnly,
        Constants.Risk.Reversible => RiskClass.Reversible,
        Constants.Risk.Risky => RiskClass.Risky,
        Constants.Risk.Irreversible => RiskClass.Irreversible,
        _ => RiskClass.Risky
    };

    public static bool RequiresHuman(RiskClass risk) =>
        risk is RiskClass.Risky or RiskClass.Irreversible;

    public static bool IsApproved(CapabilityArtifact artifact) =>
        artifact.ApprovalState.Equals(Constants.Approval.Approved, StringComparison.OrdinalIgnoreCase);

    public static ExecutionResult? CheckApproval(CapabilityArtifact artifact, bool allowDraft)
    {
        if (allowDraft || IsApproved(artifact))
            return null;
        if (!artifact.Steps.Any(s => RequiresHuman(ParseRisk(s.Risk))))
            return null;
        return new ExecutionResult
        {
            Kind = ResultKind.PolicyFailure,
            Message = $"Artifact '{artifact.Id}' is '{artifact.ApprovalState}' and contains {Constants.Risk.Risky}/{Constants.Risk.Irreversible} steps. Approve it or pass {Constants.Flag.AllowDraft}."
        };
    }

    public static ExecutionResult? CheckAction(AllowlistConfig cfg, string action, Uri? uri)
    {
        if (!cfg.AllowedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Action '{action}' is not allowlisted."
            };
        }

        if (uri is null)
            return null;

        if (!cfg.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Host '{uri.Host}' is not allowlisted."
            };
        }

        if (cfg.AllowedPorts.Count > 0 && !cfg.AllowedPorts.Contains(uri.Port))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Port '{uri.Port}' is not allowlisted."
            };
        }

        foreach (var p in cfg.ProhibitedPathPrefixes)
        {
            if (uri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                return new ExecutionResult
                {
                    Kind = ResultKind.PolicyFailure,
                    Message = $"Path '{uri.AbsolutePath}' is prohibited."
                };
            }
        }

        if (cfg.AllowedPathPrefixes.Count > 0 &&
            !cfg.AllowedPathPrefixes.Any(p => uri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return new ExecutionResult
            {
                Kind = ResultKind.PolicyFailure,
                Message = $"Path '{uri.AbsolutePath}' is not allowlisted."
            };
        }

        return null;
    }
}
