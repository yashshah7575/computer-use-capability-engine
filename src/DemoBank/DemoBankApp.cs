using ComputerUse.Domain;

namespace DemoBank;

public static class DemoBankApp
{
    public static WebApplication Build(string[] args, string? urls = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(urls ?? Constants.Network.DemoBankUrl);
        var app = builder.Build();
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });

        var members = new Dictionary<string, (string Name, decimal Savings)>
        {
            [Constants.Member.Known] = ("Doe, Jane", 1842.50m),
            [Constants.Member.Alternate] = ("Rivera, Luis", 90.00m),
            [Constants.Member.Transient] = ("Chen, Mira", 500.00m)
        };
        var interruptionShown = new HashSet<string>(StringComparer.Ordinal);

        app.MapGet("/", () => Results.Content(Page("Member lookup", $"""
            <iframe src="/notice" width="100%" height="48" style="border:0"></iframe>
            <table border="1" cellpadding="6" cellspacing="0">
              <tr><td>Institution</td><td>Demo Credit Union (synthetic)</td></tr>
              <tr>
                <td>Member number</td>
                <td>
                  <form method="get" action="/lookup">
                    <input name="memberno" size="16" />
                    <button type="submit">{Constants.Ui.Lookup}</button>
                  </form>
                </td>
              </tr>
            </table>
            """), "text/html"));

        app.MapGet("/notice", () => Results.Content(
            "<p>Back-office console — synthetic data only.</p>", "text/html"));

        app.MapGet("/lookup", async (string? memberno) =>
        {
            await Task.Delay(Constants.Timing.WaitStepMilliseconds);
            if (memberno == Constants.Member.Transient && interruptionShown.Add(Constants.Member.Transient))
            {
                return Results.Content(Page(Constants.Ui.Lookup, $"""
                    <p>{Constants.Outcome.InterruptionText}</p>
                    <p>A temporary back-office interruption occurred.</p>
                    <p><a href="/lookup?memberno={Constants.Member.Transient}">{Constants.Ui.Dismiss}</a></p>
                    """), "text/html");
            }

            if (string.IsNullOrWhiteSpace(memberno) || !members.ContainsKey(memberno))
            {
                return Results.Content(Page(Constants.Ui.Lookup, $"""
                    <table border="1" cellpadding="6"><tr><td>{Constants.Outcome.MemberNotFoundText}</td></tr></table>
                    <p><a href="/">Back</a></p>
                    """), "text/html");
            }

            var m = members[memberno];
            var html = $"""
                <table border="1" cellpadding="6" cellspacing="0">
                  <tr><th>ID</th><th>Name</th></tr>
                  <tr><td>{memberno}</td><td><a href="/member?id={memberno}">{m.Name}</a></td></tr>
                </table>
                """;
            return Results.Content(Page("Search results", html), "text/html");
        });

        app.MapGet("/member", (string? id) =>
        {
            if (id is null || !members.TryGetValue(id, out var m))
                return Results.Content(Page("Error", "<p>Application Error</p>"), "text/html");

            var html = $"""
                <h2>{Constants.Ui.MemberRecord}</h2>
                <table border="1" cellpadding="6" cellspacing="0">
                  <tr><td>Name</td><td>{m.Name}</td></tr>
                  <tr><td>Savings</td><td>{m.Savings:0.00}</td></tr>
                </table>
                <p><a href="/subaccount?id={id}">{Constants.Ui.OpenSubAccount}</a></p>
                """;
            return Results.Content(Page(Constants.Ui.MemberRecord, html), "text/html");
        });

        app.MapGet("/subaccount", (string? id) =>
        {
            var html = $"""
                <h2>{Constants.Ui.OpenSubAccount}</h2>
                <p>This step is irreversible in production.</p>
                <form method="post" action="/subaccount">
                  <input type="hidden" name="id" value="{id}" />
                  <button type="submit" name="confirm" value="yes">{Constants.Ui.ConfirmOpenSubAccount}</button>
                </form>
                """;
            return Results.Content(Page("Confirm", html), "text/html");
        });

        app.MapPost("/subaccount", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            return Results.Content(Page("Done", $"<p>Sub-account opened for {form["id"]}.</p>"), "text/html");
        });

        app.MapGet("/admin", () => Results.Text("denied"));
        return app;
    }

    private static string Page(string title, string body) => $"""
        <!doctype html><html><head><meta charset="utf-8"><title>{title}</title></head>
        <body><table width="100%"><tr><td bgcolor="#335"><font color="white">{title}</font></td></tr></table>
        {body}</body></html>
        """;
}
