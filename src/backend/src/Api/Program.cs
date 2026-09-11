using Api;
using Api.Endpoints.Health;
using Api.Extensions;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

if (OpenApiExport.Requested(args))
{
    // Reading the document requires a built host, and the host refuses to start
    // without valid configuration. Placeholders satisfy that on this path only;
    // nothing is connected to and no request is served.
    builder.Configuration.AddInMemoryCollection(OpenApiExport.PlaceholderConfiguration());
}

builder.AddObservability();

builder.Services
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

if (OpenApiExport.Requested(args))
{
    await OpenApiExport.WriteAsync(app, args).ConfigureAwait(false);

    return;
}

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

// 11. UseAuthentication  — cookie to principal
// 12. UseSessionContext  — principal to session record, user id on scope and span
// 13. UseCsrfGuard       — needs the session to compare the token against
// 14. UseAuthorization   — policies, after identity is fully established
//
// Positions 11-14 are added by the identity beads (see `bd ready`). They belong
// here, in this order, and nothing above them may move to accommodate them.

app.MapHealthEndpoints();
app.MapEndpoints();
app.MapSinglePageAppFallback();

await app.RunAsync().ConfigureAwait(false);
