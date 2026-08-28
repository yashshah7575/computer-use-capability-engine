using System.Text.Json;

namespace ComputerUse.Domain;

public static class ArtifactSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string Serialize(CapabilityArtifact artifact) =>
        JsonSerializer.Serialize(artifact, JsonOptions);

    public static CapabilityArtifact Deserialize(string json)
    {
        var artifact = JsonSerializer.Deserialize<CapabilityArtifact>(json, JsonOptions)
            ?? throw new InvalidOperationException("Artifact JSON deserialized to null.");
        Validate(artifact);
        return artifact;
    }

    public static void Validate(CapabilityArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.SchemaVersion))
            throw new InvalidOperationException("schemaVersion is required.");
        if (string.IsNullOrWhiteSpace(artifact.Id))
            throw new InvalidOperationException("id is required.");
        if (artifact.Steps.Count == 0)
            throw new InvalidOperationException("steps must not be empty.");
        var approval = artifact.ApprovalState.Trim();
        if (!approval.Equals(Constants.Approval.Draft, StringComparison.OrdinalIgnoreCase) &&
            !approval.Equals(Constants.Approval.Approved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"approvalState must be '{Constants.Approval.Draft}' or '{Constants.Approval.Approved}'.");
        artifact.ApprovalState = approval.ToLowerInvariant();

        foreach (var outcome in artifact.KnownOutcomes)
        {
            if (string.IsNullOrWhiteSpace(outcome.Code))
                throw new InvalidOperationException("Each knownOutcome needs a code.");
            if (string.IsNullOrWhiteSpace(outcome.TextContains) && string.IsNullOrWhiteSpace(outcome.UrlContains))
                throw new InvalidOperationException($"knownOutcome '{outcome.Code}' needs textContains or urlContains.");
        }

        foreach (var condition in artifact.RecoverableConditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Code))
                throw new InvalidOperationException("Each recoverableCondition needs a code.");
            if (string.IsNullOrWhiteSpace(condition.TextContains))
                throw new InvalidOperationException($"recoverableCondition '{condition.Code}' needs textContains.");
            var action = condition.Action.Trim().ToLowerInvariant();
            if (action is not (Constants.Recovery.Dismiss or Constants.Recovery.Wait))
                throw new InvalidOperationException(
                    $"recoverableCondition '{condition.Code}' action must be {Constants.Recovery.Dismiss} or {Constants.Recovery.Wait}.");
            condition.Action = action;
            if (condition.MaxRetries < 1)
                throw new InvalidOperationException($"recoverableCondition '{condition.Code}' maxRetries must be >= 1.");
        }

        foreach (var step in artifact.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || string.IsNullOrWhiteSpace(step.Action))
                throw new InvalidOperationException("Each step needs id and action.");
        }
    }
}
