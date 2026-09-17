using Domain.Import;
using Domain.Shared;

namespace Domain.UnitTests.Import;

/// <summary>What a connection will and will not accept.</summary>
public class RecipeSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldNameItselfAfterItsHost_WhenNobodySaysOtherwise()
    {
        // Act
        var source = Connect(label: null);

        // Assert
        // What it is, and what tells two of them apart — which is more than an
        // empty name or a generic "Tandoor" would do.
        Assert.Equal("recipes.example.com", source.Label);
    }

    [Fact]
    public void Create_ShouldNotHaveBeenUsed_UntilRecipesAreTakenFromIt()
    {
        // Act
        var source = Connect();

        // Assert
        // The difference between "connected" and "used": one is worth offering
        // again, the other is something somebody set up and forgot.
        Assert.Null(source.LastUsedAt);
    }

    [Fact]
    public void Used_ShouldRecordWhen_AndCountAsAWrite()
    {
        // Arrange
        var source = Connect();
        var later = Now.AddHours(1);

        // Act
        source.Used(later);

        // Assert
        Assert.Equal(later, source.LastUsedAt);
        Assert.Equal(2, source.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRefuse_AConnectionWithNoToken(string? token)
    {
        // Act
        var source = Create(token: token);

        // Assert
        Assert.Equal(
            ImportErrors.InvalidSourceToken.Code,
            source.Match(value => value.Label, error => error.Code));
    }

    [Fact]
    public void Create_ShouldRefuse_ATokenLongEnoughToBeAPastedFile()
    {
        // Act
        var source = Create(token: new string('t', RecipeSource.MaxSecretLength + 1));

        // Assert
        Assert.Equal(
            ImportErrors.InvalidSourceToken.Code,
            source.Match(value => value.Label, error => error.Code));
    }

    [Fact]
    public void Create_ShouldRefuse_AKindNobodyCanConnectTo()
    {
        // Act
        // A web page is a kind an origin can have, not a kind a source can be:
        // there is nothing there to connect to.
        var source = Create(kind: SourceKind.Web);

        // Assert
        Assert.Equal(
            ImportErrors.UnknownSourceKind.Code,
            source.Match(value => value.Label, error => error.Code));
    }

    [Fact]
    public void Create_ShouldRefuse_ANameLongerThanALabel()
    {
        // Act
        var source = Create(label: new string('n', RecipeSource.MaxLabelLength + 1));

        // Assert
        Assert.Equal(
            ImportErrors.InvalidSourceLabel.Code,
            source.Match(value => value.Label, error => error.Code));
    }

    private static RecipeSource Connect(string? label = "Kitchen") =>
        Create(label: label).Match(
            value => value,
            error => throw new InvalidOperationException(error.Code));

    private static Result<RecipeSource> Create(
        SourceKind? kind = null,
        string? label = "Kitchen",
        string? token = "tda_secret")
    {
        var address = SourceAddress.Create("https://recipes.example.com")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        return RecipeSource.Create(
            Guid.NewGuid(),
            kind ?? SourceKind.Tandoor,
            label,
            address,
            token,
            Guid.NewGuid(),
            Now);
    }
}
