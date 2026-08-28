using ComputerUse.Domain;
using Microsoft.Playwright;

namespace ComputerUse.Surfaces.Playwright;

public interface ISurfaceDriver : IAsyncDisposable
{
    Task NavigateAsync(string url);
    Task<string> ObserveAsync();
    Task ClickAsync(IReadOnlyList<LocatorSpec> locators);
    Task TypeAsync(IReadOnlyList<LocatorSpec> locators, string text);
    Task<string> ExtractAsync(IReadOnlyList<LocatorSpec> locators);
    Task<bool> PageContainsAsync(string text);
    Task ScreenshotAsync(string path);
    Task<string> UrlAsync();
    IPage Page { get; }
}

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

    public async Task NavigateAsync(string url) => await Page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

    public async Task<string> ObserveAsync()
    {
        var title = await Page.TitleAsync();
        var body = await Page.InnerTextAsync("body");
        return $"URL={Page.Url}\nTITLE={title}\n{body}";
    }

    public async Task ClickAsync(IReadOnlyList<LocatorSpec> locators)
    {
        var loc = await ResolveAsync(locators);
        await loc.ClickAsync();
    }

    public async Task TypeAsync(IReadOnlyList<LocatorSpec> locators, string text)
    {
        var loc = await ResolveAsync(locators);
        await loc.FillAsync(text);
    }

    public async Task<string> ExtractAsync(IReadOnlyList<LocatorSpec> locators)
    {
        var loc = await ResolveAsync(locators);
        return (await loc.InnerTextAsync()).Trim();
    }

    public async Task<bool> PageContainsAsync(string text) =>
        (await Page.ContentAsync()).Contains(text, StringComparison.OrdinalIgnoreCase);

    public async Task ScreenshotAsync(string path) =>
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });

    public Task<string> UrlAsync() => Task.FromResult(Page.Url);

    public async Task SaveTraceAsync(string path) =>
        await _ctx.Tracing.StopAsync(new() { Path = path });

    public async ValueTask DisposeAsync()
    {
        try { await _ctx.Tracing.StopAsync(); } catch { /* ignore */ }
        await _ctx.CloseAsync();
        await _browser.CloseAsync();
        _pw.Dispose();
    }

    private async Task<ILocator> ResolveAsync(IReadOnlyList<LocatorSpec> locators)
    {
        Exception? last = null;
        foreach (var spec in locators)
        {
            try
            {
                ILocator loc = spec.Strategy.ToLowerInvariant() switch
                {
                    "role" => Page.GetByRole(ParseRole(spec.Role), new() { Name = spec.Name }),
                    "text" => Page.GetByText(spec.Value ?? spec.Name ?? ""),
                    "placeholder" => Page.GetByPlaceholder(spec.Value ?? ""),
                    _ => Page.Locator(spec.Value ?? "body")
                };
                await loc.First.WaitForAsync(new() { Timeout = 5000 });
                return loc.First;
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
}
