using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// DemoBank environment/policy knowledge. These are not discovered by the LLM; they are attached
/// to emitted artifacts so replay can classify known business outcomes and recoveries.
/// </summary>
public static class DemoBankEnvironmentKnowledge
{
    public static List<KnownOutcome> KnownOutcomes() =>
    [
        new() { Code = Constants.Outcome.MemberNotFound, TextContains = Constants.Outcome.MemberNotFoundText }
    ];

    public static List<RecoverableCondition> RecoverableConditions() =>
    [
        new()
        {
            Code = Constants.Outcome.TransientInterruption,
            TextContains = Constants.Outcome.InterruptionText,
            Action = Constants.Recovery.Dismiss,
            MaxRetries = 1,
            Locators = [new() { Strategy = Constants.Locator.Text, Value = Constants.Ui.Dismiss }]
        }
    ];
}
