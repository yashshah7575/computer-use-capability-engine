using ComputerUse.Domain;

namespace ComputerUse.Handoff;

public sealed class HandoffHost : IAsyncDisposable
{
    private TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _app;
    public InterventionRequest? Current { get; private set; }
    public ControllerKind Controller { get; private set; } = ControllerKind.Automation;

    public async Task StartAsync(int port = 5200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        _app = builder.Build();
        _app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });
        _app.MapGet("/", () => Results.Content(Html(), "text/html"));
        _app.MapPost("/resume", () =>
        {
            Controller = ControllerKind.Automation;
            _resume.TrySetResult();
            return Results.Redirect("/");
        });
        await _app.StartAsync();
    }

    public void Preview(InterventionRequest req)
    {
        Current = req;
        Controller = ControllerKind.Human;
    }

    public async Task WaitForHumanAsync(InterventionRequest req)
    {
        Current = req;
        Controller = ControllerKind.Human;
        _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Console.WriteLine($"HITL: {req.Reason}  Open http://127.0.0.1:5200 then use the headed browser and press Resume.");
        await _resume.Task;
    }

    private string Html()
    {
        var r = Current;
        var shot = r?.ScreenshotPath is { } p && File.Exists(p)
            ? $"<p>Screenshot saved: {p}</p>"
            : "<p>No screenshot yet.</p>";
        return $"""
            <!doctype html><html><head><meta charset="utf-8"><title>Operator</title></head>
            <body>
            <h1>Human intervention</h1>
            <p>Controller: {Controller}</p>
            <p>Run: {r?.RunId}</p>
            <p>Step: {r?.StepId}</p>
            <p>Reason: {r?.Reason}</p>
            {shot}
            <p>Use the already-open DemoBank browser window, then:</p>
            <form method="post" action="/resume"><button type="submit">Resume automation</button></form>
            </body></html>
            """;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
