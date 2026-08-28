using ComputerUse.Agent;

namespace ComputerUse.Tests.Fakes;

internal sealed class ScriptedLanguageModel : ILanguageModel
{
    private readonly Queue<string> _replies;

    public ScriptedLanguageModel(params string[] replies) =>
        _replies = new Queue<string>(replies);

    public Task<string> CompleteAsync(string prompt)
    {
        if (_replies.Count == 0)
            throw new InvalidOperationException("No scripted model replies remain.");
        return Task.FromResult(_replies.Dequeue());
    }
}
