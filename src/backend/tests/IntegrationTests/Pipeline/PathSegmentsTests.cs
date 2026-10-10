using Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IntegrationTests.Pipeline;

public class PathSegmentsTests
{
    [Theory]
    [InlineData("/api/v1/households/x/ingredients/Salz%2FPfeffer", "Salz/Pfeffer")]
    [InlineData("/api/v1/households/x/ingredients/100%25%20Saft%20%26%20Mark", "100% Saft & Mark")]
    [InlineData("/api/v1/households/x/ingredients/M%C3%BCsli?x=%2F", "Müsli")]
    [InlineData("/api/v1/households/x/ingredients/a%252Fb", "a%2Fb")]
    public void LastDecoded_ShouldSplitTheRawTargetBeforeDecoding(string target, string expected)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpRequestFeature>(new HttpRequestFeature { RawTarget = target });

        // Act
        var name = PathSegments.LastDecoded(context, "ignored");

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void LastDecoded_ShouldDecodeOnlyTheSlash_WhenThereIsNoRawTarget()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var name = PathSegments.LastDecoded(context, "Salz%2fPfeffer");

        // Assert
        Assert.Equal("Salz/Pfeffer", name);
    }
}
