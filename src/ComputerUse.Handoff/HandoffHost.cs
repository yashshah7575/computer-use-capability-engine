using ComputerUse.Domain;

namespace ComputerUse.Handoff;

public sealed class HandoffHost : IAsyncDisposable
{
    private TaskCompletionSource<HumanGateOutcome> _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _app;
    private Func<Task<IReadOnlyList<HumanAction>>>? _peekActions;
    private string? _resumeMessage;
    private HumanGateOutcome _accepted = new();

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
        _app.MapGet("/", async () => Results.Content(await HtmlAsync(), "text/html"));
        _app.MapGet(Constants.Route.Screenshot, () =>
        {
            var path = Current?.ScreenshotPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Results.NotFound();
            return Results.File(path, "image/png");
        });
        _app.MapPost(Constants.Route.Authorize, () => CompleteAsync(HumanGateDecision.AuthorizeAutomation));
        _app.MapPost(Constants.Route.Completed, () => CompleteAsync(HumanGateDecision.CompletedByHuman));
        _app.MapPost(Constants.Route.Deny, () => CompleteAsync(HumanGateDecision.Denied));
        await _app.StartAsync();
    }

    public void Preview(InterventionRequest req)
    {
        Current = req;
        Controller = ControllerKind.Human;
    }

    public async Task<HumanGateOutcome> WaitForHumanAsync(
        InterventionRequest req,
        Func<Task<IReadOnlyList<HumanAction>>> peekActions)
    {
        Current = req;
        Controller = ControllerKind.Human;
        _peekActions = peekActions;
        _resumeMessage = null;
        _accepted = new HumanGateOutcome();
        _resume = new TaskCompletionSource<HumanGateOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        Console.WriteLine($"HITL: {req.Reason}  Open {Constants.Network.LoopbackUrl(_port)} and choose Authorize, Completed, or Deny.");
        var outcome = await _resume.Task;
        return outcome;
    }

    private async Task<IResult> CompleteAsync(HumanGateDecision decision)
    {
        IReadOnlyList<HumanAction> actions = [];
        if (_peekActions is not null)
            actions = await _peekActions();
        _accepted = new HumanGateOutcome { Decision = decision, Actions = actions.ToList() };
        _resumeMessage = null;
        if (decision == HumanGateDecision.AuthorizeAutomation)
            ResumeAutomation();
        _resume.TrySetResult(_accepted);
        return Results.Redirect("/");
    }

    public void ResumeAutomation() => Controller = ControllerKind.Automation;

    private async Task<string> HtmlAsync()
    {
        var r = Current;
        var shot = r?.ScreenshotPath is { } p && File.Exists(p)
            ? $"<p><img src=\"{Constants.Route.Screenshot}\" alt=\"Session screenshot\" style=\"max-width:100%;border:1px solid #ccc\" /></p>"
            : "<p>No screenshot yet.</p>";
        var refused = string.IsNullOrEmpty(_resumeMessage)
            ? ""
            : $"<p><strong>{_resumeMessage}</strong></p>";
        IReadOnlyList<HumanAction> live = [];
        if (_peekActions is not null)
        {
            try { live = await _peekActions(); }
            catch { live = []; }
        }

        var audit = live.Count == 0
            ? "<p>No live-session actions captured yet (audit only; they do not authorize the step).</p>"
            : "<ul>" + string.Join("", live.Select(a => $"<li>{a.Kind}: {a.Detail}</li>")) + "</ul>";
        return $"""
            <!doctype html><html><head><meta charset="utf-8"><title>Operator</title></head>
            <body>
            <h1>Human intervention</h1>
            <p>Controller: {Controller}</p>
            <p>Run: {r?.RunId}</p>
            <p>Step: {r?.StepId}</p>
            <p>Reason: {r?.Reason}</p>
            {refused}
            <p><strong>Choose one explicit decision.</strong> The picture below is a screenshot, not the live bank. Use the already-open Chromium DemoBank window for bank clicks.</p>
            {audit}
            <form method="post" action="{Constants.Route.Authorize}"><button type="submit">Authorize automation to perform this step</button></form>
            <form method="post" action="{Constants.Route.Completed}"><button type="submit">I completed the step</button></form>
            <form method="post" action="{Constants.Route.Deny}"><button type="submit">Deny / stop</button></form>
            {shot}
            </body></html>
            """;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
