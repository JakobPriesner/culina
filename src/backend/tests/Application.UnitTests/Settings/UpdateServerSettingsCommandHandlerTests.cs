using Application.Abstractions.Settings;
using Application.Settings;
using Application.Settings.UpdateServer;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// Saving server settings: validated as startup validates, only the difference written, a restart
/// only when there is one.
/// </summary>
public class UpdateServerSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheProposalIsWhatTheServerRunsWith()
    {
        var world = new World();

        var change = (await world.Handle(Command())).ShouldBeSuccess();

        Assert.Equal(ServerChange.None, change);
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldSaveOnlyTheDifference_AndRestart()
    {
        var world = new World();

        var change = (await world.Handle(Command(limits: new RateLimitSettings { ImportsPerHour = 45 })))
            .ShouldBeSuccess();

        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal(1, world.Restart.Scheduled);
        Assert.Equal(new Dictionary<string, string> { ["RateLimits:ImportsPerHour"] = "45" }, world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldWriteAClearedEndpointAsEmpty_SoItOverridesOneSetBelowTheFile()
    {
        var world = new World(new TelemetrySettings { Endpoint = new Uri("http://collector:4317") });

        await world.Handle(Command(endpoint: null));

        Assert.Equal(string.Empty, world.Configuration.Saved![TelemetrySettings.EndpointKey]);
    }

    [Fact]
    public async Task Handle_ShouldNotRestart_WhenTheSettingsCannotBeSaved()
    {
        var world = new World();
        world.Configuration.Writable = false;

        var result = await world.Handle(Command(limits: new RateLimitSettings { ImportsPerHour = 45 }));

        result.ShouldBeFailure(SettingsErrors.NotWritable);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRefuseWhatTheStartupWouldRefuse()
    {
        var world = new World();

        var result = await world.Handle(Command(
            cookies: new CookieSettings { SessionDays = 0 },
            endpoint: "collector without a scheme"));

        var failure = Assert.IsType<ValidationError>(result.ShouldBeFailure());
        Assert.Equal(2, failure.Errors.Count);
        Assert.Null(world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldRefuseInsecureCookies_WhenTheDeploymentDoesNotAllowThem()
    {
        var world = new World();

        var result = await world.Handle(Command(cookies: new CookieSettings { Secure = false }));

        result.ShouldBeFailure(SettingsErrors.InsecureCookies);
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRefuseInsecureCookies_EvenWhenTheProposalClaimsTheyAreAllowed()
    {
        var world = new World();

        var result = await world.Handle(Command(cookies: new CookieSettings { Secure = false, InsecureAllowed = true }));

        result.ShouldBeFailure(SettingsErrors.InsecureCookies);
    }

    [Fact]
    public async Task Handle_ShouldSaveInsecureCookies_WhenTheDeploymentAllowsThem()
    {
        var world = new World(cookies: new CookieSettings { InsecureAllowed = true });

        var change = (await world.Handle(Command(cookies: new CookieSettings { Secure = false }))).ShouldBeSuccess();

        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal(new Dictionary<string, string> { ["Cookies:Secure"] = "false" }, world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAProxyNetworkTooWideToTrust_WithACodeOfItsOwn()
    {
        var world = new World();

        var result = await world.Handle(Command(
            proxies: new ForwardedHeadersSettings { KnownNetworks = ["0.0.0.0/0"] }));

        // Its own code, so the screen can say why.
        result.ShouldBeFailure(SettingsErrors.ProxyNetworkTooWide("0.0.0.0/0"));
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldSaveAWideProxyNetwork_WhenTheDeploymentTrustsOneByName()
    {
        // The screen never offers the override; a deployment that set it has saves validated as its
        // next startup will.
        var world = new World(proxies: new ForwardedHeadersSettings { DangerouslyTrustWideNetworks = true });

        var change = (await world.Handle(Command(
                proxies: new ForwardedHeadersSettings { KnownNetworks = ["0.0.0.0/0"] })))
            .ShouldBeSuccess();

        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal("0.0.0.0/0", world.Configuration.Saved!["ForwardedHeaders:KnownNetworks"]);
    }

    private static UpdateServerSettingsCommand Command(
        CookieSettings? cookies = null,
        RateLimitSettings? limits = null,
        string? endpoint = null,
        ForwardedHeadersSettings? proxies = null) =>
        new(
            cookies ?? new CookieSettings(),
            proxies ?? new ForwardedHeadersSettings(),
            limits ?? new RateLimitSettings(),
            endpoint,
            "grpc");

    private sealed class World(
        TelemetrySettings? telemetry = null,
        CookieSettings? cookies = null,
        ForwardedHeadersSettings? proxies = null)
    {
        public FakeServerConfiguration Configuration { get; } = new();

        public RecordingRestart Restart { get; } = new();

        public Task<Result<ServerChange>> Handle(UpdateServerSettingsCommand command) =>
            new UpdateServerSettingsCommandHandler(
                    cookies ?? new CookieSettings(),
                    proxies ?? new ForwardedHeadersSettings(),
                    new RateLimitSettings(),
                    telemetry ?? new TelemetrySettings(),
                    Configuration,
                    Restart)
                .Handle(command, Token);
    }
}
