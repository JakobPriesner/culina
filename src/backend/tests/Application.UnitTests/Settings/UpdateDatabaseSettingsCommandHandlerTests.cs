using Application.Settings;
using Application.Settings.UpdateDatabase;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// Pointing the instance at a database: tried first, saved second, applied by
/// a restart — and nothing at all when any step says no.
/// </summary>
public class UpdateDatabaseSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldSaveOnlyWhatChanged_AndRestart_WhenTheConnectionWorks()
    {
        // Arrange
        var world = new World();

        // Act
        var change = (await world.Handle(Command(host: "db.internal"))).ShouldBeSuccess();

        // Assert
        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal(1, world.Restart.Scheduled);
        Assert.Equal(new Dictionary<string, string> { ["Database:Host"] = "db.internal" }, world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldTryTheCurrentPassword_WhenNoNewOneIsGiven()
    {
        // Arrange
        var world = new World();

        // Act
        await world.Handle(Command(host: "db.internal", password: null));

        // Assert
        // Write-only: the form never has the password to send back, so leaving
        // the field empty has to mean "keep it", not "clear it".
        Assert.Equal("secret", world.Check.Tried!.Password);
        Assert.False(world.Configuration.Saved!.ContainsKey("Database:Password"));
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenTheDatabaseCannotBeReached()
    {
        // Arrange
        var world = new World();
        world.Check.FailWith = SettingsErrors.DatabaseUnreachable("password authentication failed");

        // Act
        var result = await world.Handle(Command(host: "db.internal"));

        // Assert
        Assert.Equal("settings.database_unreachable", result.ShouldBeFailure().Code);
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldNotTryTheConnection_WhenTheSettingsCouldNeverBeSaved()
    {
        // Arrange
        var world = new World();
        world.Configuration.Writable = false;

        // Act
        var result = await world.Handle(Command(host: "db.internal"));

        // Assert
        result.ShouldBeFailure(SettingsErrors.NotWritable);
        Assert.Null(world.Check.Tried);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenEveryValueIsWhatItRunsWith()
    {
        // Arrange
        var world = new World();

        // Act
        var change = (await world.Handle(Command())).ShouldBeSuccess();

        // Assert
        Assert.Equal(ServerChange.None, change);
        Assert.Null(world.Check.Tried);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenThereIsNoPasswordAtAll()
    {
        // Arrange
        var world = new World(new Dictionary<string, string>());

        // Act
        var result = await world.Handle(Command(password: null));

        // Assert
        Assert.Equal("settings.invalid_value", result.ShouldBeFailure().Code);
    }

    private static UpdateDatabaseSettingsCommand Command(string host = "localhost", string? password = "secret") =>
        new(host, 5432, "culina", "culina_app", password, RequireSsl: false, MaxPoolSize: 20);

    private sealed class World(IReadOnlyDictionary<string, string>? running = null)
    {
        public FakeServerConfiguration Configuration { get; } = new(running ?? new Dictionary<string, string>
        {
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "culina",
            ["Database:Username"] = "culina_app",
            ["Database:Password"] = "secret",
            ["Database:RequireSsl"] = "false"
        });

        public FakeDatabaseConnectionCheck Check { get; } = new();

        public RecordingRestart Restart { get; } = new();

        public Task<Result<ServerChange>> Handle(UpdateDatabaseSettingsCommand command) =>
            new UpdateDatabaseSettingsCommandHandler(Configuration, Check, Restart).Handle(command, Token);
    }
}
