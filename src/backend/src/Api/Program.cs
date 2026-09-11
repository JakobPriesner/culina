using Api;
using Api.Extensions;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

// A singleton that captured a scoped service is a bug that otherwise surfaces
// as an intermittent failure in production. Fail at startup instead.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = builder.Environment.IsDevelopment();
    options.ValidateOnBuild = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// The order of this pipeline is the contract, not a preference. Moving a line
// is a security change and needs a test, not a review comment. Each entry says
// why it sits where it does.
// ─────────────────────────────────────────────────────────────────────────────

app.UseRequestContext();      // 2. Correlation id first, so every later line carries it.
app.UseSecurityHeaders();     // 3. Set before any handler can begin writing a body.
app.UseExceptionHandler();    // 5. Outside everything below, so any defect becomes a problem document.
app.UseProblemStatusPages();  // 6. Framework-generated statuses get a problem body too.

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

await app.RunAsync().ConfigureAwait(false);
