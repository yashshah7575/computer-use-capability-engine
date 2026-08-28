using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// Records actions that actually succeeded during discovery and emits a draft capability artifact.
/// Does not use <see cref="DiscoveryAgent.ScriptedLookup"/>.
/// </summary>
internal sealed class DiscoveryRecorder
{
    private readonly DiscoveryContext _context;
    private readonly DiscoverySpecification _spec;
    private readonly List<ArtifactStep> _steps = [];
    private readonly List<TypedField> _outputs = [];
    private int _index;

    public DiscoveryRecorder(DiscoveryContext context, DiscoverySpecification? spec = null)
    {
        _context = context;
        _spec = spec ?? DiscoverySpecification.LookupSavingsBalance;
    }

    public IReadOnlyList<ArtifactStep> Steps => _steps;

    public void RecordNavigate()
    {
        _steps.Add(new ArtifactStep
        {
            Id = NextId(Constants.Action.Navigate),
            Action = Constants.Action.Navigate,
            Url = Constants.Template.BaseUrlRoot,
            Risk = Constants.Risk.ReadOnly
        });
    }

    public void Record(ArtifactStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Id))
            step.Id = NextId(step.Action);
        if (string.IsNullOrWhiteSpace(step.Risk))
            step.Risk = RiskFor(step.Action);
        if (step.Action == Constants.Action.Extract &&
            _steps.LastOrDefault() is { Action: Constants.Action.Extract } prev &&
            string.Equals(prev.ExtractName, step.ExtractName, StringComparison.OrdinalIgnoreCase))
            return;
        _steps.Add(step);
    }

    public void RecordOutput(string name, string type)
    {
        if (_outputs.Any(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;
        _outputs.Add(new TypedField { Name = name, Type = type });
    }

    public string PersistTypeValue(ModelAction action)
    {
        var parameter = action.Parameter?.Trim();
        if (!string.IsNullOrEmpty(parameter) && DiscoveryContext.SupportedParameters.Contains(parameter))
            return "{{" + parameter + "}}";

        var concrete = ConcreteTypeValue(action);
        foreach (var kv in _context.KnownInputs)
        {
            if (concrete.Equals(kv.Value, StringComparison.Ordinal))
                return "{{" + kv.Key + "}}";
        }

        return concrete;
    }

    public string ConcreteTypeValue(ModelAction action)
    {
        var parameter = action.Parameter?.Trim();
        if (!string.IsNullOrEmpty(parameter) &&
            _context.KnownInputs.TryGetValue(parameter, out var known))
            return known;
        if (!string.IsNullOrEmpty(parameter) &&
            !DiscoveryContext.SupportedParameters.Contains(parameter))
            return parameter;
        if (!string.IsNullOrEmpty(action.Value))
            return action.Value;
        return action.Text ?? "";
    }

    public static string RiskFor(string action) =>
        action == Constants.Action.Type ? Constants.Risk.Reversible : Constants.Risk.ReadOnly;

    public CapabilityArtifact Emit()
    {
        if (!_steps.Any(s => s.Action is Constants.Action.Click or Constants.Action.Type or Constants.Action.Extract))
            throw new InvalidOperationException("Discovery produced no meaningful recorded actions.");
        if (_spec.RequireCheckpoint && !_steps.Any(s => s.Action == Constants.Action.Checkpoint))
            throw new InvalidOperationException("Discovery cannot emit an artifact without a recorded checkpoint.");

        var inputs = new List<TypedField>
        {
            new() { Name = Constants.Field.BaseUrl, Type = Constants.Field.StringType }
        };
        if (_steps.Any(s => s.Value == Constants.Template.MemberId))
            inputs.Insert(0, new() { Name = Constants.Field.MemberId, Type = Constants.Field.StringType });

        foreach (var required in _spec.RequiredInputs)
        {
            if (!inputs.Any(i => i.Name.Equals(required, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Discovery completed without required input '{required}'.");
        }

        foreach (var required in _spec.RequiredOutputs)
        {
            if (!_outputs.Any(o => o.Name.Equals(required, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Discovery completed without required output '{required}'.");
            if (!_steps.Any(s => s.Action == Constants.Action.Extract &&
                                 string.Equals(s.ExtractName, required, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Discovery completed without required output '{required}'.");
        }

        if (_outputs.Any(o => o.Name.Equals(Constants.Field.Balance, StringComparison.OrdinalIgnoreCase) &&
                              !o.Type.Equals(Constants.Field.DecimalType, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Discovery completed without required output '{Constants.Field.Balance}'.");

        return new CapabilityArtifact
        {
            SchemaVersion = Constants.Schema.Version,
            Id = Constants.ArtifactId.LookupSavingsBalance,
            Description = _context.Goal,
            ArtifactVersion = 1,
            ApprovalState = Constants.Approval.Draft,
            Inputs = inputs,
            Outputs = _outputs,
            KnownOutcomes = DemoBankEnvironmentKnowledge.KnownOutcomes(),
            RecoverableConditions = DemoBankEnvironmentKnowledge.RecoverableConditions(),
            Steps = [.. _steps]
        };
    }

    public bool TryEmit(out CapabilityArtifact artifact)
    {
        try
        {
            artifact = Emit();
            return true;
        }
        catch (InvalidOperationException)
        {
            artifact = null!;
            return false;
        }
    }

    private string NextId(string action)
    {
        _index++;
        return $"disc-{_index:00}-{action}";
    }
}
