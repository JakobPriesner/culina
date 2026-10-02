using Api.Extensions;
using Api.Infrastructure;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Pipeline;

/// <summary>
/// What a log line is allowed to contain.
/// </summary>
/// <remarks>
/// An operator reads these; so does anybody who ends up with the file. A
/// request body is either somebody's recipe or somebody's password, and
/// neither belongs in a line that outlives the request.
/// </remarks>
public class LoggingTests
{
    [Fact]
    public void RequestLogging_ShouldRecordNoBody_InAnyEnvironment()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddRequestLogging();

        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<HttpLoggingOptions>>().Value;

        // Assert
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestBody));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseBody));

        // Headers are out too: a cookie header is a session, and an
        // Authorization header would be a credential.
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestHeaders));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseHeaders));

        // And the query string, which is where an invitation code would appear
        // if one were ever put there.
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestQuery));
    }

    [Fact]
    public void Scope_ShouldReadAsItsValues_WhereAConsoleFormatterPrintsIt()
    {
        // Arrange
        var scope = new LogScope(new("TraceId", "4bf92f35"), new("ClientAddress", "203.0.113.7"));

        // Act
        var printed = scope.ToString();

        // Assert
        // A dictionary printed its type name here, on every production line.
        Assert.Equal("TraceId:4bf92f35 ClientAddress:203.0.113.7", printed);
        Assert.Equal(["TraceId", "ClientAddress"], scope.Select(value => value.Key));
    }
}
