namespace ComputerUse.Domain;

public enum ResultKind
{
    Success,
    BusinessOutcome,
    Recoverable,
    HardFailure,
    PolicyFailure,
    InterventionRequired
}
