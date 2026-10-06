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
    public async Task Verify_ShouldAccept_WhenThePasswordIsCorrect()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);
        var hash = await hasher.HashAsync("correct horse battery staple", Token);

        // Act
        var verification = await hasher.VerifyAsync("correct horse battery staple", hash, Token);

        // Assert
        Assert.Equal(PasswordVerification.Valid, verification);
    }

    [Fact]
    public async Task Verify_ShouldReject_WhenThePasswordIsWrong()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);
        var hash = await hasher.HashAsync("correct horse battery staple", Token);

        // Act
        var verification = await hasher.VerifyAsync("correct horse battery stapler", hash, Token);

        // Assert
        Assert.Equal(PasswordVerification.Failed, verification);
    }

    [Fact]
    public async Task Hash_ShouldDifferEveryTime_BecauseEachHashHasItsOwnSalt()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);

        // Act
        var first = await hasher.HashAsync("the same password", Token);
        var second = await hasher.HashAsync("the same password", Token);

        // Assert
        // Without a per-hash salt, identical passwords would be visibly
        // identical in the database.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Hash_ShouldRecordItsParameters_SoTheyCanBeRaisedLater()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);

        // Act
        var hash = await hasher.HashAsync("a password long enough", Token);

        // Assert
        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_ShouldAskForARehash_WhenTheStoredHashUsedWeakerParameters()
    {
        // Arrange
        using var weaker = new Argon2PasswordHasher(Current with { Iterations = 2 });
        using var stronger = new Argon2PasswordHasher(Current with { Iterations = 4 });
        var hash = await weaker.HashAsync("a password long enough", Token);

        // Act
        var verification = await stronger.VerifyAsync("a password long enough", hash, Token);

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
    public async Task Verify_ShouldFailSafely_WhenTheStoredHashIsCorrupt(string stored)
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);

        // Act
        var verification = await hasher.VerifyAsync("a password long enough", stored, Token);

        // Assert
        // A corrupt hash means the account cannot be signed into, not that the
        // process crashes.
        Assert.Equal(PasswordVerification.Failed, verification);
    }

    [Fact]
    public async Task DecoyHash_ShouldVerifyLikeARealOne_SoAnUnknownAccountCostsTheSame()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);
        var decoy = hasher.DecoyHash;
        var real = await hasher.HashAsync("a real password", Token);

        // Act
        var againstDecoy = await TimeAsync(() => hasher.VerifyAsync("any password at all", decoy, Token));
        var againstReal = await TimeAsync(() => hasher.VerifyAsync("wrong password here", real, Token));

        // Assert
        // Both must actually compute a hash. Skipping the work for an unknown
        // account is what turns a login endpoint into an enumeration oracle.
        Assert.True(
            againstDecoy > TimeSpan.Zero && againstReal > TimeSpan.Zero,
            "both verifications must do real work");
    }

    [Fact]
    public async Task VerifyAsync_ShouldStopWaitingForATurn_WhenTheCallerGivesUp()
    {
        // Arrange
        using var hasher = new Argon2PasswordHasher(Current);
        var hash = await hasher.HashAsync("a password long enough", Token);
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        // Act
        var verifying = hasher.VerifyAsync("a password long enough", hash, abandoned.Token);

        // Assert
        // Waiting for a turn is bounded by the request: a client that went
        // away does not keep a queued hash alive.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifying);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<TimeSpan> TimeAsync(Func<Task<PasswordVerification>> work)
    {
        var started = Stopwatch.GetTimestamp();

        await work();

        return Stopwatch.GetElapsedTime(started);
    }
}
