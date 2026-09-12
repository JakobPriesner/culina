using Api;
using Api.Endpoints.Health;
using Api.Extensions;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// The export starts the host, so it needs somewhere to listen — but not the
// port the app uses, or exporting the contract would be impossible while the
// app is running, which is exactly when it is usually done. Port 0 is whatever
// is free; no request is ever served on it.
if (OpenApiExport.Requested(args))
{
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

builder.AddObservability();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation()
    .AddEndpoints();

// A singleton that captured a scoped service is a bug that otherwise surfaces
// as an intermittent failure in production. Fail at startup instead.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = builder.Environment.IsDevelopment();
    options.ValidateOnBuild = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// THE ORDER OF THIS PIPELINE IS THE CONTRACT, not a preference. Moving a line
// is a security change: it needs a test, not a review comment. Each entry says
// why it sits where it does, and IntegrationTests/Pipeline asserts the
// properties that depend on the ordering.
// ─────────────────────────────────────────────────────────────────────────────

app.UseCulinaForwardedHeaders();  //  1. Real client IP and scheme, before anything reads them.
app.UseRequestContext();          //  2. Correlation id, so every later line carries it.
app.UseSecurityHeaders();         //  3. Set before any handler can begin writing a body.
app.UseHttpLogging();             //  4. One combined line per request.
app.UseExceptionHandler();        //  5. Outside everything below: any defect becomes a problem document.
app.UseProblemStatusPages();      //  6. Framework-generated statuses get a problem body too.
app.UseSinglePageApp();           //  7. Static assets are cheap and never reach authentication.

// Explicit, so the checks below can read endpoint metadata. Relying on the
// implicit UseRouting would leave the position of a security check to a
// framework detail.
app.UseRouting();

app.UseQueryParameterGuard();     //  8. Reject unknown or repeated input before binding.
app.UseSameOriginGuard();         //  9. Unsafe cookie-authenticated requests must come from us.
app.UseRateLimiter();             // 10. Before authentication: brute force costs nothing to reject.

app.UseAuthentication();          // 11. Cookie to principal.

app.UseSessionContext();          // 12. User id on the logging scope and the span.
app.UseCsrfGuard();               // 13. Needs the session to compare the token against.
app.UseAuthorization();           // 14. Policies, after identity is established.

app.MapHealthEndpoints();
app.MapEndpoints();
app.MapSinglePageAppFallback();

// After the endpoints are mapped: the document is built by enumerating them,
// so exporting any earlier produces an empty file.
if (OpenApiExport.Requested(args))
{
    await OpenApiExport.WriteAsync(app, args).ConfigureAwait(false);

    return;
}

await app.RunAsync().ConfigureAwait(false);
