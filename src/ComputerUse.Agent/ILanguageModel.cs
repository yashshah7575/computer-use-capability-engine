namespace ComputerUse.Agent;

/// <summary>
/// Language-model completion used during discovery. Implementations wrap a provider SDK.
/// </summary>
public interface ILanguageModel
{
    /// <summary>Returns a single model completion for <paramref name="prompt"/>.</summary>
    /// <param name="prompt">Discovery observation and instructions.</param>
    /// <returns>Raw model text; callers parse a JSON tool object from it.</returns>
    Task<string> CompleteAsync(string prompt);
}
