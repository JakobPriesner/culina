using System.Text.Json;
using Api.Infrastructure;
using Domain.Shared;
using Microsoft.AspNetCore.Http;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// The problem document is the frontend's only error format, so its shape is
/// part of the API contract.
/// </summary>
public class ProblemResultTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.RateLimited, 429)]
    [InlineData(ErrorType.Unavailable, 503)]
    [InlineData(ErrorType.Failure, 500)]
    [InlineData(ErrorType.Problem, 500)]
    public async Task Problem_ShouldUseTheStatusCodeForTheErrorType_WhenWritten(
        ErrorType type,
        int expectedStatus)
    {
        // Arrange
        var error = new Error("tests.example", "Something specific happened.", type);
        var context = ContextWithBody();

        // Act
        await CustomResults.Problem(error).ExecuteAsync(context);

        // Assert
        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    [Fact]
    public async Task Problem_ShouldCarryTheCodeAndRequestId_WhenTheContextHasOne()
    {
        // Arrange
        var error = new Error("users.email_already_used", "Already registered.", ErrorType.Conflict);
        var context = ContextWithBody();
        RequestContext.SetRequestId(context, "abc123");

        // Act
        await CustomResults.Problem(error).ExecuteAsync(context);

        // Assert
        var document = await ReadBodyAsync(context);
        Assert.Equal("users.email_already_used", document.GetProperty("code").GetString());
        Assert.Equal("abc123", document.GetProperty("requestId").GetString());
        Assert.Equal("urn:culina:problem:users.email_already_used", document.GetProperty("type").GetString());
        Assert.Equal("Conflict", document.GetProperty("title").GetString());
        Assert.Equal("Already registered.", document.GetProperty("detail").GetString());
        Assert.StartsWith("application/problem+json", context.Response.ContentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Problem_ShouldListEveryCause_WhenTheErrorIsAValidationAggregate()
    {
        // Arrange
        var error = new ValidationError(
        [
            new FieldError("title", "recipes.invalid_title", "A title is required."),
            new FieldError("yield", "recipes.invalid_yield", "Must be greater than zero."),
            new Error("recipes.too_many_steps", "At most 100 steps.", ErrorType.Validation)
        ]);
        var context = ContextWithBody();

        // Act
        await CustomResults.Problem(error).ExecuteAsync(context);

        // Assert
        var causes = (await ReadBodyAsync(context)).GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal(3, causes.Count);
        Assert.Equal("title", causes[0].GetProperty("field").GetString());
        // A cause that names no field is still reported rather than dropped.
        Assert.Equal(JsonValueKind.Null, causes[2].GetProperty("field").ValueKind);
        Assert.Equal("recipes.too_many_steps", causes[2].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Problem_ShouldOmitTheRequestId_WhenThePipelineHasNotAssignedOne()
    {
        // Arrange
        var error = new Error("tests.example", "No correlation yet.", ErrorType.Failure);
        var context = ContextWithBody();

        // Act
        await CustomResults.Problem(error).ExecuteAsync(context);

        // Assert
        var document = await ReadBodyAsync(context);
        Assert.False(document.TryGetProperty("requestId", out _));
    }

    private static DefaultHttpContext ContextWithBody() =>
        new() { Response = { Body = new MemoryStream() } };

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }
}
