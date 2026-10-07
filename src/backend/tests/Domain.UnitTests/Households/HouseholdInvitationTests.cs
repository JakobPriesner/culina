using Domain.Households;

namespace Domain.UnitTests.Households;

/// <summary>
/// An invitation is a bearer token: whoever holds the code can join. It is hashed, works once and
/// expires, and each limit has to hold on its own.
/// </summary>
public class HouseholdInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsUsable_ShouldBeTrue_ForAFreshUnusedCode()
    {
        var invitation = Invitation(expiresAt: Now.AddDays(7), redeemedBy: null);

        var usable = invitation.IsUsable(Now);

        Assert.True(usable);
    }

    [Fact]
    public void IsUsable_ShouldBeFalse_OnceItHasBeenRedeemed()
    {
        // Otherwise a code forwarded out of a group chat lets in everybody who
        // saw it, not the one person it was sent to.
        var invitation = Invitation(expiresAt: Now.AddDays(7), redeemedBy: Guid.NewGuid());

        var usable = invitation.IsUsable(Now);

        Assert.False(usable);
    }

    [Fact]
    public void IsUsable_ShouldBeFalse_OnceItHasExpired()
    {
        // A link in a message somebody scrolls past for a year is not consent
        // given a year later.
        var invitation = Invitation(expiresAt: Now.AddSeconds(-1), redeemedBy: null);

        var usable = invitation.IsUsable(Now);

        Assert.False(usable);
    }

    [Fact]
    public void IsUsable_ShouldBeFalse_AtTheInstantItExpires()
    {
        // The boundary belongs to the past: an expiry that is still usable at
        // the moment it names is an expiry nobody can reason about.
        var invitation = Invitation(expiresAt: Now, redeemedBy: null);

        var usable = invitation.IsUsable(Now);

        Assert.False(usable);
    }

    private static HouseholdInvitation Invitation(DateTimeOffset expiresAt, Guid? redeemedBy) =>
        HouseholdInvitation.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            System.Text.Encoding.UTF8.GetBytes("a-hash-of-the-code"),
            Guid.NewGuid(),
            Now.AddDays(-1),
            expiresAt,
            redeemedBy,
            redeemedBy is null ? null : Now);
}
