namespace ComputerUse.Domain;

/// <summary>
/// Perceive/act seam. Surface adapters implement this; replay and discovery depend only on the contract.
/// </summary>
public interface ISurfaceDriver : IAsyncDisposable
{
    /// <summary>Navigates the surface to <paramref name="url"/>.</summary>
    /// <param name="url">Absolute URL to open.</param>
    Task NavigateAsync(string url);

    /// <summary>Returns a text observation of the current surface (URL, title, visible text).</summary>
    /// <returns>A redaction-safe observation string.</returns>
    Task<string> ObserveAsync();

    /// <summary>Clicks the first uniquely matching locator in <paramref name="locators"/>.</summary>
    /// <param name="locators">Fallback chain; ambiguity at a tier fails that tier.</param>
    /// <returns>Which locator index resolved.</returns>
    Task<LocatorMatch> ClickAsync(IReadOnlyList<LocatorSpec> locators);

    /// <summary>Types <paramref name="text"/> into the first uniquely matching locator.</summary>
    /// <param name="locators">Fallback chain for the input control.</param>
    /// <param name="text">Value to enter (already substituted parameters).</param>
    /// <returns>Which locator index resolved.</returns>
    Task<LocatorMatch> TypeAsync(IReadOnlyList<LocatorSpec> locators, string text);

    /// <summary>Extracts inner text from the first uniquely matching locator.</summary>
    /// <param name="locators">Fallback chain for the extraction target.</param>
    /// <returns>Extracted text and which locator index resolved.</returns>
    Task<ExtractResult> ExtractAsync(IReadOnlyList<LocatorSpec> locators);

    /// <summary>Returns whether any locator in the chain currently resolves uniquely.</summary>
    /// <param name="locators">Fallback chain to probe without acting.</param>
    Task<bool> CanResolveAsync(IReadOnlyList<LocatorSpec> locators);

    /// <summary>Returns whether the current page content contains <paramref name="text"/> (case-insensitive).</summary>
    /// <param name="text">Substring to search for.</param>
    Task<bool> PageContainsAsync(string text);

    /// <summary>Writes a screenshot of the current surface to <paramref name="path"/>.</summary>
    /// <param name="path">Destination file path.</param>
    Task ScreenshotAsync(string path);

    /// <summary>Returns the current surface URL.</summary>
    Task<string> UrlAsync();

    /// <summary>Starts recording click/type events on the live session for HITL audit.</summary>
    Task StartHumanAuditAsync();

    /// <summary>Returns human actions recorded since <see cref="StartHumanAuditAsync"/>.</summary>
    Task<IReadOnlyList<HumanAction>> PeekHumanAuditAsync();
}
