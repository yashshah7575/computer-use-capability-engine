namespace ComputerUse.Domain;

/// <summary>
/// Explicit operator choice for a RISKY/IRREVERSIBLE step. Arbitrary session clicks are not a decision.
/// </summary>
public enum HumanGateDecision
{
    Denied = 0,
    AuthorizeAutomation,
    CompletedByHuman
}
