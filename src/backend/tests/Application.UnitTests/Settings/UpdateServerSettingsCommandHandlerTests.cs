using Application.Abstractions.Settings;
using Application.Settings;
using Application.Settings.UpdateServer;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// Saving server settings: validated as the startup validates, only the
/// difference written, and a restart only when there is one.
/// </summary>
public class UpdateServerSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheProposalIsWhatTheServerRunsWith()
    {
        // Arrange
        var world = new World();

        // Act
        var change = (await world.Handle(Command())).ShouldBeSuccess();

        // Assert
        Assert.Equal(ServerChange.None, change);
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldSaveOnlyTheDifference_AndRestart()
    {
        // Arrange
        var world = new World();

        // Act
        var change = (await world.Handle(Command(limits: new RateLimitSettings { ImportsPerHour = 45 })))
            .ShouldBeSuccess();

        // Assert
        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal(1, world.Restart.Scheduled);
        Assert.Equal(new Dictionary<string, string> { ["RateLimits:ImportsPerHour"] = "45" }, world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldWriteAClearedEndpointAsEmpty_SoItOverridesOneSetBelowTheFile()
    {
        // Arrange
        var world = new World(new TelemetrySettings { Endpoint = new Uri("http://collector:4317") });

        // Act
        await world.Handle(Command(endpoint: null));

        // Assert
        Assert.Equal(string.Empty, world.Configuration.Saved![TelemetrySettings.EndpointKey]);
    }

    [Fact]
    public async Task Handle_ShouldNotRestart_WhenTheSettingsCannotBeSaved()
    {
        // Arrange
        var world = new World();
        world.Configuration.Writable = false;

        // Act
        var result = await world.Handle(Command(limits: new RateLimitSettings { ImportsPerHour = 45 }));

        // Assert
        result.ShouldBeFailure(SettingsErrors.NotWritable);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRefuseWhatTheStartupWouldRefuse()
    {
        // Arrange
        var world = new World();

        // Act
        var result = await world.Handle(Command(
            cookies: new CookieSettings { SessionDays = 0 },
            endpoint: "collector without a scheme"));

        // Assert
        var failure = Assert.IsType<ValidationError>(result.ShouldBeFailure());
        Assert.Equal(2, failure.Errors.Count);
        Assert.Null(world.Configuration.Saved);
    }

    private static UpdateServerSettingsCommand Command(
        CookieSettings? cookies = null,
        RateLimitSettings? limits = null,
        string? endpoint = null) =>
        new(cookies ?? new CookieSettings(), new ForwardedHeadersSettings(), limits ?? new RateLimitSettings(), endpoint, "grpc");

    private sealed class World(TelemetrySettings? telemetry = null)
    {
        public FakeServerConfiguration Configuration { get; } = new();

        public RecordingRestart Restart { get; } = new();

        public Task<Result<ServerChange>> Handle(UpdateServerSettingsCommand command) =>
            new UpdateServerSettingsCommandHandler(
                    new CookieSettings(),
                    new ForwardedHeadersSettings(),
                    new RateLimitSettings(),
                    telemetry ?? new TelemetrySettings(),
                    Configuration,
                    Restart)
                .Handle(command, Token);
    }
}
