using System.Text.Json;
using Api.Infrastructure;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The guard exists so a mistyped filter fails loudly: silently ignoring it
/// would return everything while the caller believes it filtered.
/// </summary>
public class QueryParameterGuardTests
{
    [Fact]
    public async Task Request_ShouldBeRejected_WhenItCarriesAnUndeclaredParameter()
    {
        // Arrange
        var context = Request("?unexpected=1", single: ["query"], repeatable: []);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("request.unknown_parameter", await CodeAsync(context));
    }

    [Fact]
    public async Task Request_ShouldBeRejected_WhenASingleValuedParameterIsRepeated()
    {
        // Arrange
        var context = Request("?query=a&query=b", single: ["query"], repeatable: []);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.False(reached);
        Assert.Equal("request.repeated_parameter", await CodeAsync(context));
    }

    [Fact]
    public async Task Request_ShouldBeAccepted_WhenARepeatableParameterIsRepeated()
    {
        // Arrange
        // Repeating a tag means "both", so it is declared repeatable.
        var context = Request("?tag=vegan&tag=quick", single: [], repeatable: ["tag"]);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.True(reached);
    }

    [Fact]
    public async Task Request_ShouldBeAccepted_WhenEveryParameterIsDeclared()
    {
        // Arrange
        var context = Request("?query=pasta&tag=vegan", single: ["query"], repeatable: ["tag"]);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.True(reached);
    }

    [Fact]
    public async Task Request_ShouldPassThrough_WhenNoEndpointMatchedThePath()
    {
        // Arrange
        var context = Request("?anything=1", single: [], repeatable: [], withEndpoint: false);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // Routing's own 404 is more useful than blaming a parameter on an
        // address that does not exist.
        Assert.True(reached);
    }

    [Fact]
    public async Task Request_ShouldPassThrough_WhenThePathIsNotAnApiPath()
    {
        // Arrange
        var context = Request("?anything=1", single: [], repeatable: [], path: "/recipes");

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // The SPA's own routes carry whatever query the app puts there.
        Assert.True(reached);
    }

    private static DefaultHttpContext Request(
        string queryString,
        IReadOnlyCollection<string> single,
        IReadOnlyCollection<string> repeatable,
        bool withEndpoint = true,
        string path = "/api/v1/recipes")
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);

        if (withEndpoint)
        {
            context.Features.Set<IEndpointFeature>(new TestEndpointFeature(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new AllowedQueryParameters(single, repeatable)),
                "test")));
        }

        return context;
    }

    private static async Task<bool> InvokeAsync(HttpContext context)
    {
        var reached = false;

        var middleware = new QueryParameterGuardMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, NullLogger<QueryParameterGuardMiddleware>.Instance);

        return reached;
    }

    private static async Task<string?> CodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var document = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        return document.GetProperty("code").GetString();
    }

    private sealed class TestEndpointFeature(Endpoint endpoint) : IEndpointFeature
    {
        public Endpoint? Endpoint { get; set; } = endpoint;
    }
}
