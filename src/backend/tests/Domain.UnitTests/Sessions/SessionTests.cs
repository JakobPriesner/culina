using Domain.Sessions;

namespace Domain.UnitTests.Sessions;

/// <summary>The sliding expiry that keeps somebody signed in on a device they use; the cookie is an opaque reference, so renewal is a property of the row.</summary>
public class SessionTests
{
    private static readonly DateTimeOffset SignedInAt = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan Ceiling = TimeSpan.FromDays(90);
    private static readonly TimeSpan Daily = TimeSpan.FromHours(24);

    [Fact]
    public void Touch_ShouldMoveTheExpiryForward_SoDailyUseNeverLapses()
    {
        var session = NewSession();
        var threeWeeksLater = SignedInAt.AddDays(21);

        session.Touch(threeWeeksLater, Lifetime, Ceiling);

        Assert.Equal(threeWeeksLater.Add(Lifetime), session.ExpiresAt);
        Assert.Equal(threeWeeksLater, session.LastSeenAt);
        Assert.True(session.IsActive(SignedInAt.AddDays(45)));
    }

    [Fact]
    public void Touch_ShouldStopAtTheCeiling_HoweverOftenTheSessionIsUsed()
    {
        var session = NewSession();

        for (var day = 1; day <= 100; day++)
        {
            session.Touch(SignedInAt.AddDays(day), Lifetime, Ceiling);
        }

        // A stolen cookie used every day used to live for ever.
        Assert.Equal(SignedInAt.Add(Ceiling), session.ExpiresAt);
        Assert.False(session.IsActive(SignedInAt.Add(Ceiling)));
    }

    [Fact]
    public void Touch_ShouldOnlyShortenTheLastStretch_WhenTheCeilingIsNear()
    {
        var session = NewSession();
        var lateInLife = SignedInAt.AddDays(80);

        session.Touch(lateInLife, Lifetime, Ceiling);

        Assert.Equal(SignedInAt.Add(Ceiling), session.ExpiresAt);
        Assert.Equal(lateInLife, session.LastSeenAt);
    }

    [Fact]
    public void IsDueForRenewal_ShouldSayNo_WhenTheSessionWasJustUsed()
    {
        var session = NewSession();

        var due = session.IsDueForRenewal(SignedInAt.AddMinutes(5), Daily);

        // Every page view would otherwise cost a write.
        Assert.False(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldSayYes_OnceTheIntervalHasPassed()
    {
        var session = NewSession();

        var due = session.IsDueForRenewal(SignedInAt.Add(Daily), Daily);

        Assert.True(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldMeasureFromTheLastUse_NotFromSignIn()
    {
        var session = NewSession();
        session.Touch(SignedInAt.AddDays(10), Lifetime, Ceiling);

        var due = session.IsDueForRenewal(SignedInAt.AddDays(10).AddHours(1), Daily);

        Assert.False(due);
    }

    [Fact]
    public void IsDueForRenewal_ShouldAlwaysSayYes_WhenTheIntervalIsZero()
    {
        var session = NewSession();

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
