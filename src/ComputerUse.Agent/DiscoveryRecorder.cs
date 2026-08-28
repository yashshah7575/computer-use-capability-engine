using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// Records actions that actually succeeded during discovery and emits a draft capability artifact.
/// Does not use <see cref="DiscoveryAgent.ScriptedLookup"/>.
/// </summary>
internal sealed class DiscoveryRecorder
{
    private readonly DiscoveryContext _context;
    private readonly List<ArtifactStep> _steps = [];
    private readonly List<TypedField> _outputs = [];
    private int _index;

    public DiscoveryRecorder(DiscoveryContext context) => _context = context;

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
        if (!string.IsNullOrEmpty(parameter))
        {
            if (!DiscoveryContext.SupportedParameters.Contains(parameter))
                throw new InvalidOperationException($"Unsupported discovery parameter '{parameter}'.");
            return "{{" + parameter + "}}";
        }

        var concrete = action.Value ?? action.Text ?? "";
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
        if (!_steps.Any(s => s.Action == Constants.Action.Checkpoint))
            throw new InvalidOperationException("Discovery cannot emit an artifact without a recorded checkpoint.");
        foreach (var output in _outputs)
        {
            if (!_steps.Any(s => s.Action == Constants.Action.Extract &&
                                 string.Equals(s.ExtractName, output.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Declared output '{output.Name}' has no extract step.");
        }

        var inputs = new List<TypedField>
        {
            new() { Name = Constants.Field.BaseUrl, Type = Constants.Field.StringType }
        };
        if (_steps.Any(s => s.Value == Constants.Template.MemberId))
            inputs.Insert(0, new() { Name = Constants.Field.MemberId, Type = Constants.Field.StringType });

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

    private string NextId(string action)
    {
        _index++;
        return $"disc-{_index:00}-{action}";
    }
}
