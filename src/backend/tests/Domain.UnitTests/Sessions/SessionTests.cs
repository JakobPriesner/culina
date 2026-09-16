using Domain.Sessions;

namespace Domain.UnitTests.Sessions;

/// <summary>
/// The sliding expiry, which is what keeps somebody signed in on a device they
/// use. Culina issues no refresh token — the cookie is an opaque reference, so
/// renewal is a property of the row rather than an exchange.
/// </summary>
public class SessionTests
{
    private static readonly DateTimeOffset SignedInAt = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan Daily = TimeSpan.FromHours(24);

    [Fact]
    public void Touch_ShouldMoveTheExpiryForward_SoDailyUseNeverLapses()
    {
        // Arrange
        var session = NewSession();
        var threeWeeksLater = SignedInAt.AddDays(21);

        // Act
        session.Touch(threeWeeksLater, Lifetime);

        // Assert
        Assert.Equal(threeWeeksLater.Add(Lifetime), session.ExpiresAt);
        Assert.Equal(threeWeeksLater, session.LastSeenAt);
        Assert.True(session.IsActive(SignedInAt.AddDays(45)));
    }

    [Fact]
    public void IsDueForRenewal_ShouldSayNo_WhenTheSessionWasJustUsed()
    {
        // Arrange
        var session = NewSession();

        // Act
        var due = session.IsDueForRenewal(SignedInAt.AddMinutes(5), Daily);

        // Assert
        // Every page view would otherwise cost a write, and a session used
        // twice in a minute is no more alive than one used once.
        Assert.False(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldSayYes_OnceTheIntervalHasPassed()
    {
        // Arrange
        var session = NewSession();

        // Act
        var due = session.IsDueForRenewal(SignedInAt.Add(Daily), Daily);

        // Assert
        Assert.True(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldMeasureFromTheLastUse_NotFromSignIn()
    {
        // Arrange
        var session = NewSession();
        session.Touch(SignedInAt.AddDays(10), Lifetime);

        // Act
        var due = session.IsDueForRenewal(SignedInAt.AddDays(10).AddHours(1), Daily);

        // Assert
        Assert.False(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldAlwaysSayYes_WhenTheIntervalIsZero()
    {
        // Arrange
        var session = NewSession();

        // Act & Assert
        Assert.True(session.IsDueForRenewal(SignedInAt, TimeSpan.Zero));
    }

    private static Session NewSession() =>
        Session.Start(
            Guid.CreateVersion7(),
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            SignedInAt,
            Lifetime,
            ipAddress: null,
            userAgent: null);
}
