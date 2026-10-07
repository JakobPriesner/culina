using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The first line an operator reads when something is wrong: which build, and where it keeps its
/// data.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class StartupLogTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Startup_ShouldSayWhereTheDataIs_WithoutTheDatabasePassword()
    {
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.NewApiClient();

        await client.GetAsync("/health", TestContext.Current.CancellationToken);

        var line = Assert.Single(factory.Logs.Lines, line => line.EventId == 1602);
        Assert.Equal(postgres.Settings.Name, line["DatabaseName"]);
        Assert.Equal("Production", line["Environment"]);
        Assert.Equal("nowhere", line["TelemetryExport"]);
        Assert.DoesNotContain(postgres.Settings.Password, line.Message, StringComparison.Ordinal);
    }
}
