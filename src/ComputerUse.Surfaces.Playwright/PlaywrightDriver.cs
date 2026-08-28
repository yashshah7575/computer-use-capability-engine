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
        var raw = await Page.EvaluateAsync<ControlDto[]>(ControlScanScript) ?? [];
        var controls = raw.Select(c => new ObservedControl
        {
            Tag = c.Tag ?? "",
            Role = c.Role,
            Name = c.Name,
            Text = c.Text,
            Label = c.Label,
            Placeholder = c.Placeholder,
            InputName = c.InputName,
            Type = c.Type,
            Href = c.Href
        }).ToList();
        return ObservationFormatter.Format(Page.Url, title, Redaction.Redact(body), controls);
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
                    Constants.Locator.Label => Page.GetByLabel(spec.Value ?? spec.Name ?? ""),
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

    private static AriaRole ParseRole(string? role)
    {
        var value = (role ?? "").Trim();
        if (value.Equals("input", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("textbox", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("searchbox", StringComparison.OrdinalIgnoreCase))
            return AriaRole.Textbox;
        if (value.Equals("a", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("link", StringComparison.OrdinalIgnoreCase))
            return AriaRole.Link;
        return Enum.TryParse<AriaRole>(value, true, out var parsed) ? parsed : AriaRole.Button;
    }

    private const string ControlScanScript = """
        () => {
          const visible = (el) => {
            const s = getComputedStyle(el);
            if (s.display === 'none' || s.visibility === 'hidden' || s.opacity === '0') return false;
            const r = el.getBoundingClientRect();
            return r.width > 0 && r.height > 0;
          };
          const labelFor = (el) => {
            if (el.id) {
              const lab = document.querySelector('label[for="' + el.id.replace(/"/g, '') + '"]');
              if (lab) return (lab.innerText || '').trim().slice(0, 80);
            }
            const wrapped = el.closest('label');
            if (wrapped) return (wrapped.innerText || '').trim().slice(0, 80);
            const tag = el.tagName.toLowerCase();
            if (tag === 'button' || tag === 'a') return '';
            const cell = el.closest('td,th');
            const prev = cell && cell.previousElementSibling;
            if (prev) return (prev.innerText || '').trim().slice(0, 80);
            return '';
          };
          const impliedRole = (el) => {
            const explicit = el.getAttribute('role');
            if (explicit) return explicit;
            const tag = el.tagName.toLowerCase();
            if (tag === 'button' || el.getAttribute('type') === 'submit') return 'button';
            if (tag === 'a') return 'link';
            if (tag === 'input' || tag === 'textarea') return 'textbox';
            if (tag === 'select') return 'combobox';
            return '';
          };
          return [
            ...[...document.querySelectorAll('a, button, input, select, textarea')].filter(visible),
            ...[...document.querySelectorAll('table tr')].filter(tr => {
              const tds = tr.querySelectorAll('td,th');
              return tds.length >= 2 && visible(tr);
            })
          ]
            .slice(0, 24)
            .map(el => {
              if (el.tagName.toLowerCase() === 'tr') {
                const tds = [...el.querySelectorAll('td,th')];
                return {
                  tag: 'td',
                  role: 'cell',
                  name: '',
                  text: ((tds[tds.length - 1].innerText || '') + '').trim().slice(0, 80),
                  label: ((tds[0].innerText || '') + '').trim().slice(0, 80),
                  placeholder: '',
                  inputName: '',
                  type: '',
                  href: ''
                };
              }
              return {
                tag: el.tagName.toLowerCase(),
                role: impliedRole(el),
                name: (el.getAttribute('aria-label') || '').trim(),
                text: ((el.innerText || el.value || '') + '').trim().slice(0, 80),
                label: labelFor(el),
                placeholder: el.getAttribute('placeholder') || '',
                inputName: el.getAttribute('name') || '',
                type: el.getAttribute('type') || '',
                href: el.getAttribute('href') || ''
              };
            });
        }
        """;

    private sealed class ControlDto
    {
        [JsonPropertyName("tag")]
        public string? Tag { get; set; }
        [JsonPropertyName("role")]
        public string? Role { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        [JsonPropertyName("placeholder")]
        public string? Placeholder { get; set; }
        [JsonPropertyName("inputName")]
        public string? InputName { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("href")]
        public string? Href { get; set; }
    }

    private sealed class HumanActionDto
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }
}
