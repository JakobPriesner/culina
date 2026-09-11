using Domain.Users;
using TestSupport;

namespace Domain.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_ShouldStartAtVersionOne_SoTheFirstETagIsStable()
    {
        // Arrange
        var email = Email.Create("ada@example.com").ShouldBeSuccess();
        var name = DisplayName.Create("Ada").ShouldBeSuccess();

        // Act
        var user = User.Register(email, name, "argon2id$hash", Now);

        // Assert
        Assert.Equal(1, user.Version);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Register_ShouldGiveEachUserItsOwnIdentifier_WhenSeveralAreCreated()
    {
        // Arrange
        var email = Email.Create("ada@example.com").ShouldBeSuccess();
        var name = DisplayName.Create("Ada").ShouldBeSuccess();

        // Act
        var first = User.Register(email, name, "hash", Now);
        var second = User.Register(email, name, "hash", Now);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("correct horse battery staple")]
    [InlineData("123456789012")]
    public void EnsureAcceptablePassword_ShouldPass_WhenItIsLongEnough(string password)
    {
        // Arrange & Act
        var result = User.EnsureAcceptablePassword(password);

        // Assert
        result.ShouldBeSuccess();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678901")]
    public void EnsureAcceptablePassword_ShouldFail_WhenItIsTooShort(string? password)
    {
        // Arrange & Act
        var result = User.EnsureAcceptablePassword(password);

        // Assert
        // Length is the only rule: composition rules push people toward
        // predictable substitutions and away from passphrases.
        result.ShouldBeFailure(UserErrors.WeakPassword);
    }

    [Fact]
    public void ChangeDisplayName_ShouldReplaceTheName_WhenGivenAValidOne()
    {
        // Arrange
        var user = AUser();
        var renamed = DisplayName.Create("Ada Lovelace").ShouldBeSuccess();

        // Act
        var result = user.ChangeDisplayName(renamed);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal("Ada Lovelace", user.DisplayName.Value);
    }

    [Fact]
    public void Restore_ShouldPreserveTheStoredVersion_SoConcurrencyStillWorks()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var email = Email.Create("ada@example.com").ShouldBeSuccess();
        var name = DisplayName.Create("Ada").ShouldBeSuccess();

        // Act
        var user = User.Restore(id, email, name, "hash", Now, version: 42);

        // Assert
        Assert.Equal(42, user.Version);
        Assert.Equal(id, user.Id);
    }

    private static User AUser() => User.Register(
        Email.Create("ada@example.com").ShouldBeSuccess(),
        DisplayName.Create("Ada").ShouldBeSuccess(),
        "argon2id$hash",
        Now);
}
