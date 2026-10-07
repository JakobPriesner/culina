using Application.Settings;
using Application.Settings.UpdateDatabase;
using Domain.Shared;
using TestSupport;

namespace Application.UnitTests.Settings;

/// <summary>
/// Pointing the instance at a database: tried first, saved second, applied by a restart, and
/// nothing at all when any step says no.
/// </summary>
public class UpdateDatabaseSettingsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldSaveOnlyWhatChanged_AndRestart_WhenTheConnectionWorks()
    {
        var world = new World();

        var change = (await world.Handle(Command(host: "db.internal"))).ShouldBeSuccess();

        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal(1, world.Restart.Scheduled);
        Assert.Equal(new Dictionary<string, string> { ["Database:Host"] = "db.internal" }, world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldTryTheCurrentPassword_WhenNoNewOneIsGivenForTheSameServer()
    {
        var world = new World();

        await world.Handle(Command(password: null, maxPoolSize: 40));

        // Write-only: the form never has the password to send back, so leaving
        // the field empty has to mean "keep it", not "clear it".
        Assert.Equal("secret", world.Check.Tried!.Password);
        Assert.False(world.Configuration.Saved!.ContainsKey("Database:Password"));
    }

    [Theory]
    [InlineData("attacker.example", 5432, "culina", "culina_app")]
    [InlineData("localhost", 6543, "culina", "culina_app")]
    [InlineData("localhost", 5432, "other", "culina_app")]
    [InlineData("localhost", 5432, "culina", "someone_else")]
    public async Task Handle_ShouldNotSendTheStoredPassword_WhenTheServerChanges(
        string host,
        int port,
        string name,
        string username)
    {
        var world = new World();

        var result = await world.Handle(
            new UpdateDatabaseSettingsCommand(host, port, name, username, null, RequireSsl: false, MaxPoolSize: 20));

        result.ShouldBeFailure(SettingsErrors.DatabasePasswordRequired);
        Assert.Null(world.Check.Tried);
        Assert.Null(world.Configuration.Saved);
    }

    [Fact]
    public async Task Handle_ShouldTryTheNewServer_WhenThePasswordIsEnteredAgain()
    {
        var world = new World();

        var change = (await world.Handle(Command(host: "db.internal", password: "secret"))).ShouldBeSuccess();

        Assert.Equal(ServerChange.Restarting, change);
        Assert.Equal("db.internal", world.Check.Tried!.Host);
    }

    [Fact]
    public async Task Handle_ShouldTryWhatTheDeploymentPins_RatherThanWhatWasSent()
    {
        var world = new World(pinned: ["Database:Host", "Database:Password"]);

        await world.Handle(Command(host: "attacker.example", password: "typed", maxPoolSize: 40));

        // The next start uses the pinned values, so those are what is tried —
        // and the pinned password never leaves for the address sent instead.
        Assert.Equal("localhost", world.Check.Tried!.Host);
        Assert.Equal("secret", world.Check.Tried!.Password);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenTheDatabaseCannotBeReached()
    {
        var world = new World();
        world.Check.FailWith = SettingsErrors.DatabaseUnreachable;

        var result = await world.Handle(Command(host: "db.internal"));

        result.ShouldBeFailure(SettingsErrors.DatabaseUnreachable);
        Assert.Null(world.Configuration.Saved);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldNotTryTheConnection_WhenTheSettingsCouldNeverBeSaved()
    {
        var world = new World();
        world.Configuration.Writable = false;

        var result = await world.Handle(Command(host: "db.internal"));

        result.ShouldBeFailure(SettingsErrors.NotWritable);
        Assert.Null(world.Check.Tried);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenEveryValueIsWhatItRunsWith()
    {
        var world = new World();

        var change = (await world.Handle(Command())).ShouldBeSuccess();

        Assert.Equal(ServerChange.None, change);
        Assert.Null(world.Check.Tried);
        Assert.Equal(0, world.Restart.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenThereIsNoPasswordAtAll()
    {
        var world = new World(new Dictionary<string, string>());

        var result = await world.Handle(Command(password: null));

        Assert.Equal("settings.invalid_value", result.ShouldBeFailure().Code);
    }

    private static UpdateDatabaseSettingsCommand Command(
        string host = "localhost",
        string? password = "secret",
        int maxPoolSize = 20) =>
        new(host, 5432, "culina", "culina_app", password, RequireSsl: false, maxPoolSize);

    private sealed class World(IReadOnlyDictionary<string, string>? running = null, IEnumerable<string>? pinned = null)
    {
        public FakeServerConfiguration Configuration { get; } = new(
            running ?? new Dictionary<string, string>
            {
                ["Database:Host"] = "localhost",
                ["Database:Port"] = "5432",
                ["Database:Name"] = "culina",
                ["Database:Username"] = "culina_app",
                ["Database:Password"] = "secret",
                ["Database:RequireSsl"] = "false"
            },
            pinned);

        public FakeDatabaseConnectionCheck Check { get; } = new();

        public RecordingRestart Restart { get; } = new();

        public Task<Result<ServerChange>> Handle(UpdateDatabaseSettingsCommand command) =>
            new UpdateDatabaseSettingsCommandHandler(Configuration, Check, Restart).Handle(command, Token);
    }
}
