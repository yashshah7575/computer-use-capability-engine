using ComputerUse.Domain;

namespace ComputerUse.Handoff;

public sealed class HandoffHost : IAsyncDisposable
{
    private TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _app;
    private Func<Task<IReadOnlyList<HumanAction>>>? _peekActions;
    private bool _requireHumanAction;
    private string? _resumeMessage;
    private List<HumanAction> _acceptedActions = [];

    public InterventionRequest? Current { get; private set; }
    public ControllerKind Controller { get; private set; } = ControllerKind.Automation;

    private int _port = Constants.Network.OperatorPort;

    public async Task StartAsync(int port = Constants.Network.OperatorPort)
    {
        _port = port;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(Constants.Network.LoopbackUrl(port));
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
        _app.MapGet(Constants.Route.Screenshot, () =>
        {
            var path = Current?.ScreenshotPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Results.NotFound();
            return Results.File(path, "image/png");
        });
        _app.MapPost(Constants.Route.Resume, async () =>
        {
            IReadOnlyList<HumanAction> actions = [];
            if (_peekActions is not null)
                actions = await _peekActions();
            if (_requireHumanAction && actions.Count == 0)
            {
                _resumeMessage = "Resume refused: no human action on the live session yet. Use the DemoBank window, then try again.";
                return Results.Content(Html(), "text/html", statusCode: 409);
            }

            _acceptedActions = actions.ToList();
            _resumeMessage = null;
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

    public async Task<IReadOnlyList<HumanAction>> WaitForHumanAsync(
        InterventionRequest req,
        Func<Task<IReadOnlyList<HumanAction>>> peekActions)
    {
        Current = req;
        Controller = ControllerKind.Human;
        _peekActions = peekActions;
        _requireHumanAction = true;
        _resumeMessage = null;
        _acceptedActions = [];
        _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Console.WriteLine($"HITL: {req.Reason}  Open {Constants.Network.LoopbackUrl(_port)} then use the headed browser and press Resume.");
        await _resume.Task;
        _requireHumanAction = false;
        return _acceptedActions;
    }

    private string Html()
    {
        var r = Current;
        var shot = r?.ScreenshotPath is { } p && File.Exists(p)
            ? $"<p><img src=\"{Constants.Route.Screenshot}\" alt=\"Session screenshot\" style=\"max-width:100%;border:1px solid #ccc\" /></p>"
            : "<p>No screenshot yet.</p>";
        var refused = string.IsNullOrEmpty(_resumeMessage)
            ? ""
            : $"<p><strong>{_resumeMessage}</strong></p>";
        return $"""
            <!doctype html><html><head><meta charset="utf-8"><title>Operator</title></head>
            <body>
            <h1>Human intervention</h1>
            <p>Controller: {Controller}</p>
            <p>Run: {r?.RunId}</p>
            <p>Step: {r?.StepId}</p>
            <p>Reason: {r?.Reason}</p>
            {refused}
            {shot}
            <p>Use the already-open DemoBank browser window (click or type at least once), then:</p>
            <form method="post" action="{Constants.Route.Resume}"><button type="submit">Resume automation</button></form>
            </body></html>
            """;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
