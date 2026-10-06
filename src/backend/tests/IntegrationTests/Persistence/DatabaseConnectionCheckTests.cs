using System.Globalization;
using System.Text;
using Application.Abstractions.Settings;
using Domain.Shared;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using TestSupport;

namespace IntegrationTests.Persistence;

/// <summary>
/// The check connects to any address it is given, during setup for anybody.
/// So it says which kind of failure it was and nothing more, and the detail
/// goes to the log.
/// </summary>
[Collection(RequiresDatabase.Name)]
public sealed class DatabaseConnectionCheckTests : IDisposable
{
    private readonly PostgresFixture postgres;
    private readonly RecordingLogs logs = new();
    private readonly ILoggerFactory loggerFactory;

    public DatabaseConnectionCheckTests(PostgresFixture postgres)
    {
        this.postgres = postgres;
        loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckAsync_ShouldSayOnlyThatNothingAnswered_WhenNothingListensOnThePort()
    {
        // Arrange
        var port = ScriptedServer.ClosedPort();

        // Act
        var result = await Check(Loopback(port));

        // Assert
        result.ShouldBeFailure(SettingsErrors.DatabaseUnreachable);
        var line = Assert.Single(logs.Lines, line => line.EventId == 1960);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal("127.0.0.1", line["DatabaseHost"]);
        Assert.Equal(port.ToString(CultureInfo.InvariantCulture), line["DatabasePort"]);
        Assert.Contains("refused", line["Reason"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_ShouldSayOnlyThatTheLoginWasRefused_WhenThePasswordIsWrong()
    {
        // Arrange
        var settings = postgres.Settings with { Password = "not the password" };

        // Act
        var result = await Check(settings);

        // Assert
        result.ShouldBeFailure(SettingsErrors.DatabaseLoginRefused);
        Assert.Contains("28P01", Assert.Single(logs.Lines)["Reason"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_ShouldSayItIsNotADatabase_WhenSomethingElseAnswers()
    {
        // Arrange
        using var web = new ScriptedServer(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));

        // Act
        var result = await Check(Loopback(web.Port));

        // Assert
        result.ShouldBeFailure(SettingsErrors.DatabaseNotPostgres);
    }

    [Fact]
    public async Task CheckAsync_ShouldSayEncryptionFailed_WhenTlsIsRequiredAndTheServerOffersNone()
    {
        // Arrange
        var settings = postgres.Settings with { RequireSsl = true };

        // Act
        var result = await Check(settings);

        // Assert
        result.ShouldBeFailure(SettingsErrors.DatabaseTlsFailed);
    }

    [Fact]
    public async Task CheckAsync_ShouldRefuseTheRole_WhenItIsASuperuser()
    {
        // Act
        var result = await Check(postgres.SuperuserSettings);

        // Assert
        result.ShouldBeFailure(SettingsErrors.DatabaseSuperuser);
    }

    [Fact]
    public async Task CheckAsync_ShouldAccept_TheApplicationsOwnRole()
    {
        // Act
        var result = await Check(postgres.Settings);

        // Assert
        result.ShouldBeSuccess();
        Assert.Empty(logs.Lines);
    }

    public void Dispose()
    {
        loggerFactory.Dispose();
        logs.Dispose();
    }

    private static DatabaseSettings Loopback(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        Name = "culina",
        Username = "culina_app",
        Password = "the stored password",
        RequireSsl = false
    };

    private Task<Result> Check(DatabaseSettings settings) =>
        new DatabaseConnectionCheck(loggerFactory.CreateLogger<DatabaseConnectionCheck>())
            .CheckAsync(settings, Token);
}
