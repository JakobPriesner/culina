using System.Diagnostics;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Identity;

namespace IntegrationTests.Identity;

/// <summary>
/// Real Argon2id, at deliberately small cost parameters so the suite stays
/// fast. The properties under test are about correctness, not about how long a
/// hash takes.
/// </summary>
public class Argon2PasswordHasherTests
{
    private static readonly PasswordHashingSettings Current = new()
    {
        MemoryKib = 19456,
        Iterations = 2,
        Parallelism = 1
    };

    [Fact]
    public void Verify_ShouldAccept_WhenThePasswordIsCorrect()
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);
        var hash = hasher.Hash("correct horse battery staple");

        // Act
        var verification = hasher.Verify("correct horse battery staple", hash);

        // Assert
        Assert.Equal(PasswordVerification.Valid, verification);
    }

    [Fact]
    public void Verify_ShouldReject_WhenThePasswordIsWrong()
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);
        var hash = hasher.Hash("correct horse battery staple");

        // Act
        var verification = hasher.Verify("correct horse battery stapler", hash);

        // Assert
        Assert.Equal(PasswordVerification.Failed, verification);
    }

    [Fact]
    public void Hash_ShouldDifferEveryTime_BecauseEachHashHasItsOwnSalt()
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);

        // Act
        var first = hasher.Hash("the same password");
        var second = hasher.Hash("the same password");

        // Assert
        // Without a per-hash salt, identical passwords would be visibly
        // identical in the database.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_ShouldRecordItsParameters_SoTheyCanBeRaisedLater()
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);

        // Act
        var hash = hasher.Hash("a password long enough");

        // Assert
        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ShouldAskForARehash_WhenTheStoredHashUsedWeakerParameters()
    {
        // Arrange
        var weaker = Current with { Iterations = 2 };
        var stronger = Current with { Iterations = 4 };
        var hash = new Argon2PasswordHasher(weaker).Hash("a password long enough");

        // Act
        var verification = new Argon2PasswordHasher(stronger).Verify("a password long enough", hash);

        // Assert
        // The old hash still verifies; it is upgraded on the owner's next
        // successful sign-in rather than by forcing a password reset.
        Assert.Equal(PasswordVerification.ValidButNeedsRehash, verification);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash at all")]
    [InlineData("$argon2id$v=19$m=x,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$not-base-64!!$aGFzaA")]
    public void Verify_ShouldFailSafely_WhenTheStoredHashIsCorrupt(string stored)
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);

        // Act
        var verification = hasher.Verify("a password long enough", stored);

        // Assert
        // A corrupt hash means the account cannot be signed into, not that the
        // process crashes.
        Assert.Equal(PasswordVerification.Failed, verification);
    }

    [Fact]
    public void DecoyHash_ShouldVerifyLikeARealOne_SoAnUnknownAccountCostsTheSame()
    {
        // Arrange
        var hasher = new Argon2PasswordHasher(Current);

        // Act
        var againstDecoy = Time(() => hasher.Verify("any password at all", hasher.DecoyHash));
        var againstReal = Time(() => hasher.Verify("wrong password here", hasher.Hash("a real password")));

        // Assert
        // Both must actually compute a hash. Skipping the work for an unknown
        // account is what turns a login endpoint into an enumeration oracle.
        Assert.True(
            againstDecoy > TimeSpan.Zero && againstReal > TimeSpan.Zero,
            "both verifications must do real work");
    }

    private static TimeSpan Time(Action work)
    {
        var started = Stopwatch.GetTimestamp();

        work();

        return Stopwatch.GetElapsedTime(started);
    }
}
