using ComputerUse.Domain;

namespace ComputerUse.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ISurfaceDriver"/> stub for unit tests (no browser).
/// </summary>
internal sealed class FakeSurfaceDriver : ISurfaceDriver
{
    public string CurrentUrl { get; set; } = Constants.Network.DemoBankUrl + "/";
    public string PageText { get; set; } = "";
    public string? Observation { get; set; }
    public string? ObservationAfterType { get; set; }
    public string? ExtractText { get; set; }
    public bool ThrowOnAct { get; set; }
    public LocatorMatch ActMatch { get; set; } = new() { MatchedIndex = 0, MatchCount = 1 };
    public bool CanResolve { get; set; } = true;
    private bool _typed;

    public Task NavigateAsync(string url)
    {
        CurrentUrl = url;
        return Task.CompletedTask;
    }

    public Task<string> ObserveAsync()
    {
        if (_typed && ObservationAfterType is not null)
            return Task.FromResult(ObservationAfterType);
        return Task.FromResult(Observation ?? $"URL={CurrentUrl}\n{PageText}");
    }

    public int ClickCount { get; private set; }

    public Task<LocatorMatch> ClickAsync(IReadOnlyList<LocatorSpec> locators)
    {
        ClickCount++;
        return ActAsync();
    }

    public Task<LocatorMatch> TypeAsync(IReadOnlyList<LocatorSpec> locators, string text)
    {
        _typed = true;
        return ActAsync();
    }

    public Task<ExtractResult> ExtractAsync(IReadOnlyList<LocatorSpec> locators) =>
        Task.FromResult(new ExtractResult { Text = ExtractText ?? PageText, Match = ActMatch });

    public Task<bool> CanResolveAsync(IReadOnlyList<LocatorSpec> locators) =>
        Task.FromResult(CanResolve);

    public Task<bool> PageContainsAsync(string text) =>
        Task.FromResult(PageText.Contains(text, StringComparison.OrdinalIgnoreCase));

    public Task ScreenshotAsync(string path) => Task.CompletedTask;

    public Task<string> UrlAsync() => Task.FromResult(CurrentUrl);

    public Task StartHumanAuditAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<HumanAction>> PeekHumanAuditAsync() =>
        Task.FromResult<IReadOnlyList<HumanAction>>([]);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<LocatorMatch> ActAsync()
    {
        if (ThrowOnAct)
            throw new InvalidOperationException("Locator failed.");
        return Task.FromResult(ActMatch);
    }
}
