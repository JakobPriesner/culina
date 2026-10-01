using Domain.Users;

namespace Domain.UnitTests.Users;

public class RecoveryCodeTests
{
    [Theory]
    [InlineData("ABCD-EFGH-JKMN-PQRS")]
    [InlineData("abcd-efgh-jkmn-pqrs")]
    [InlineData("ABCD EFGH JKMN PQRS")]
    [InlineData("  abcdefghjkmnpqrs ")]
    public void Normalise_ShouldIgnoreCaseSpacesAndDashes_BecauseTheCodeIsTypedFromPaper(string typed)
    {
        // Arrange & Act
        var normalised = RecoveryCode.Normalise(typed);

        // Assert
        Assert.Equal("ABCDEFGHJKMNPQRS", normalised);
    }

    [Fact]
    public void Normalise_ShouldReadLookAlikeLettersAsTheDigitsTheyResemble()
    {
        // Arrange & Act
        // The alphabet has no I, L or O, so a person who typed one meant 1 or 0.
        var normalised = RecoveryCode.Normalise("O0Il-1abc");

        // Assert
        Assert.Equal("00111ABC", normalised);
    }

    [Fact]
    public void Issued_ShouldExpireWithinADay_WhileASavedCodeNeverDoes()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var issued = RecoveryCode.Issued(Guid.NewGuid(), new byte[] { 1 }, Guid.NewGuid(), now);
        var saved = RecoveryCode.Saved(Guid.NewGuid(), new byte[] { 1 }, now);

        // Assert
        Assert.Equal(now.AddHours(24), issued.ExpiresAt);
        Assert.Null(saved.ExpiresAt);
        Assert.Null(saved.IssuedBy);
    }
}
