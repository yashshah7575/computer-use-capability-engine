using ComputerUse.Domain;

namespace ComputerUse.Agent;

/// <summary>
/// DemoBank vertical-slice expectations for a successful lookup-savings-balance discovery.
/// Not a general goal-to-contract compiler.
/// </summary>
internal sealed class DiscoverySpecification
{
    public IReadOnlyList<string> RequiredInputs { get; init; } = [];
    public IReadOnlyList<string> RequiredOutputs { get; init; } = [];
    public bool RequireCheckpoint { get; init; } = true;

    public static DiscoverySpecification LookupSavingsBalance { get; } = new()
    {
        RequiredInputs = [Constants.Field.MemberId],
        RequiredOutputs = [Constants.Field.Balance],
        RequireCheckpoint = true
    };
}
