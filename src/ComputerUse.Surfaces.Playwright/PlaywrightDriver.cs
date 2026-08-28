using System.Text.Json.Serialization;
using ComputerUse.Domain;
using Microsoft.Playwright;

namespace ComputerUse.Surfaces.Playwright;

public sealed class PlaywrightDriver : ISurfaceDriver
{
    private readonly IPlaywright _pw;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _ctx;

    public IPage Page { get; }

    private PlaywrightDriver(IPlaywright pw, IBrowser browser, IBrowserContext ctx, IPage page)
    {
        _pw = pw;
        _browser = browser;
        _ctx = ctx;
        Page = page;
    }

    public static async Task<PlaywrightDriver> LaunchAsync(bool headless)
    {
        var pw = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new() { Headless = headless });
        var ctx = await browser.NewContextAsync();
        await ctx.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        var page = await ctx.NewPageAsync();
        return new PlaywrightDriver(pw, browser, ctx, page);
    }

    public async Task NavigateAsync(string url) =>
        await Page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

    public async Task<string> ObserveAsync()
    {
        var title = await Page.TitleAsync();
        var body = await Page.InnerTextAsync(Constants.Selector.Body);
        return $"URL={Page.Url}\nTITLE={title}\n{body}";
    }

    public async Task<LocatorMatch> ClickAsync(IReadOnlyList<LocatorSpec> locators)
    {
        var (loc, match) = await ResolveAsync(locators);
        await loc.ClickAsync();
        return match;
    }

    public async Task<LocatorMatch> TypeAsync(IReadOnlyList<LocatorSpec> locators, string text)
    {
        var (loc, match) = await ResolveAsync(locators);
        await loc.FillAsync(text);
        return match;
    }

    public async Task<ExtractResult> ExtractAsync(IReadOnlyList<LocatorSpec> locators)
    {
        var (loc, match) = await ResolveAsync(locators);
        return new ExtractResult { Text = (await loc.InnerTextAsync()).Trim(), Match = match };
    }

    public async Task<bool> CanResolveAsync(IReadOnlyList<LocatorSpec> locators)
    {
        try
        {
            await ResolveAsync(locators, timeoutMs: Constants.Timing.ResolveProbeMilliseconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PageContainsAsync(string text) =>
        (await Page.ContentAsync()).Contains(text, StringComparison.OrdinalIgnoreCase);

    public async Task ScreenshotAsync(string path) =>
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });

    public Task<string> UrlAsync() => Task.FromResult(Page.Url);

    public async Task StartHumanAuditAsync()
    {
        await Page.EvaluateAsync($$"""
            () => {
              if (!window.__cuHumanActions) {
                window.__cuHumanActions = [];
                const rec = (kind, ev) => {
                  const el = ev.target;
                  let detail = '';
                  if (el) {
                    detail = (el.innerText || el.value || el.getAttribute?.('name') || el.tagName || '')
                      .toString().slice(0, 120);
                  }
                  window.__cuHumanActions.push({ kind, detail });
                };
                document.addEventListener('{{Constants.Action.Click}}', e => rec('{{Constants.Action.Click}}', e), true);
                document.addEventListener('input', e => rec('{{Constants.Action.Type}}', e), true);
              }
            }
            """);
    }

    public async Task<IReadOnlyList<HumanAction>> PeekHumanAuditAsync()
    {
        try
        {
            var raw = await Page.EvaluateAsync<HumanActionDto[]>("() => window.__cuHumanActions || []");
            return (raw ?? [])
                .Select(a => new HumanAction { Kind = a.Kind ?? "", Detail = Redaction.Redact(a.Detail ?? "") })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveTraceAsync(string path) =>
        await _ctx.Tracing.StopAsync(new() { Path = path });

    public async ValueTask DisposeAsync()
    {
        try { await _ctx.Tracing.StopAsync(); } catch { /* ignore */ }
        await _ctx.CloseAsync();
        await _browser.CloseAsync();
        _pw.Dispose();
    }

    private async Task<(ILocator Locator, LocatorMatch Match)> ResolveAsync(
        IReadOnlyList<LocatorSpec> locators,
        float timeoutMs = Constants.Timing.LocatorTimeoutMilliseconds)
    {
        Exception? last = null;
        for (var i = 0; i < locators.Count; i++)
        {
            var spec = locators[i];
            try
            {
                ILocator loc = spec.Strategy.ToLowerInvariant() switch
                {
                    Constants.Locator.Role => Page.GetByRole(ParseRole(spec.Role), new() { Name = spec.Name }),
                    Constants.Locator.Text => Page.GetByText(spec.Value ?? spec.Name ?? ""),
                    Constants.Locator.Placeholder => Page.GetByPlaceholder(spec.Value ?? ""),
                    _ => Page.Locator(spec.Value ?? Constants.Selector.Body)
                };
                await loc.First.WaitForAsync(new() { Timeout = timeoutMs });
                var count = await loc.CountAsync();
                if (count != 1)
                    throw new InvalidOperationException($"Ambiguous locator matched {count} nodes.");

                var strategy = spec.Strategy.ToLowerInvariant();
                if ((strategy is Constants.Locator.Css or Constants.Locator.Default || spec.Strategy.Length == 0) &&
                    !string.IsNullOrWhiteSpace(spec.Name))
                {
                    var inner = await loc.InnerTextAsync();
                    if (!inner.Contains(spec.Name, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"CSS match did not contain expected name '{spec.Name}'.");
                }

                return (loc, new LocatorMatch { MatchedIndex = i, MatchCount = count });
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException("All locators failed.", last);
    }

    private static AriaRole ParseRole(string? role) =>
        Enum.TryParse<AriaRole>(role, true, out var r) ? r : AriaRole.Button;

    private sealed class HumanActionDto
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }
}
