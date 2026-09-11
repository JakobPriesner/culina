using Api.Infrastructure;
using Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IntegrationTests.Infrastructure;

public class ETagTests
{
    [Fact]
    public void Of_ShouldProduceAStrongTagFromTheVersion_WhenFormatting()
    {
        // Arrange
        const long version = 42;

        // Act
        var tag = ETag.Of(version);

        // Assert
        Assert.Equal("\"v42\"", tag);
    }

    [Theory]
    [InlineData("\"v42\"", 42L)]
    [InlineData("v42", 42L)]
    [InlineData("W/\"v42\"", 42L)]
    [InlineData("  \"v7\"  ", 7L)]
    [InlineData("\"v0\"", 0L)]
    public void Read_ShouldRecoverTheVersion_WhenTheTagIsOneWeIssued(string header, long expected)
    {
        // Arrange & Act
        var version = ETag.Read(header);

        // Assert
        Assert.Equal(expected, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("\"abc\"")]
    [InlineData("\"v\"")]
    [InlineData("\"vnotanumber\"")]
    [InlineData("\"42\"")]
    public void Read_ShouldReturnNothing_WhenTheTagIsNotOneWeIssued(string? header)
    {
        // Arrange & Act
        var version = ETag.Read(header);

        // Assert
        Assert.Null(version);
    }

    [Fact]
    public void Ok_ShouldSetTheTagAndPrivateCaching_WhenTheCallerHasNoVersion()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = ETag.Ok(context, new { Title = "Bolognese" }, version: 3);

        // Assert
        Assert.Equal("\"v3\"", context.Response.Headers.ETag);
        Assert.Equal("private, no-cache", context.Response.Headers.CacheControl);
        Assert.IsNotType<StatusCodeHttpResult>(result);
    }

    [Fact]
    public void Ok_ShouldReturnNotModified_WhenTheCallerAlreadyHasThisVersion()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"v3\"";

        // Act
        var result = ETag.Ok(context, new { Title = "Bolognese" }, version: 3);

        // Assert
        var status = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public void Ok_ShouldReturnTheBody_WhenTheCallerHasAnOlderVersion()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"v2\"";

        // Act
        var result = ETag.Ok(context, new { Title = "Bolognese" }, version: 3);

        // Assert
        Assert.IsNotType<StatusCodeHttpResult>(result);
    }

    [Fact]
    public void RequireIfMatch_ShouldAskForThePrecondition_WhenTheHeaderIsAbsent()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = ETag.RequireIfMatch(context);

        // Assert
        // 428, not 412: the client must read and retry, and has not sent a
        // stale version.
        var error = result.Match(_ => (Error?)null, error => error);
        Assert.NotNull(error);
        Assert.Equal(ErrorType.PreconditionRequired, error.Type);
        Assert.Equal("request.precondition_required", error.Code);
    }

    [Fact]
    public void RequireIfMatch_ShouldRejectAsInvalid_WhenTheHeaderIsMalformed()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"garbage\"";

        // Act
        var result = ETag.RequireIfMatch(context);

        // Assert
        // A malformed header is a client bug, not a stale version.
        var error = result.Match(_ => (Error?)null, error => error);
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void RequireIfMatch_ShouldReturnTheVersion_WhenTheHeaderIsOneWeIssued()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = ETag.Of(9);

        // Act
        var result = ETag.RequireIfMatch(context);

        // Assert
        Assert.Equal(9L, result.Match(version => version, _ => -1L));
    }
}
