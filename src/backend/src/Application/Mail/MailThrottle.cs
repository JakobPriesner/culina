namespace Application.Mail;

/// <summary>
/// At most one mail of a kind per account per minute, however many addresses ask for it: the rate limiter counts
/// per client, and this keeps somebody with many clients from filling a stranger's inbox.
/// </summary>
/// <remarks>In memory, like the sign-in attempt budget: Culina is one process, and a restart forgetting it is harmless.</remarks>
internal sealed class MailThrottle(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Dictionary<(string Kind, Guid UserId), DateTimeOffset> sent = [];

    private readonly Lock gate = new();

    /// <summary>Whether a mail of this kind may go to this account now; true also counts it as sent.</summary>
    internal bool TryTake(string kind, Guid userId)
    {
        var now = time.GetUtcNow();

        lock (gate)
        {
            if (sent.TryGetValue((kind, userId), out var last) && now - last < Window)
            {
                return false;
            }

            // Forget what can no longer refuse anything, so the map never outgrows a minute of mail.
            foreach (var stale in sent.Where(entry => now - entry.Value >= Window).Select(entry => entry.Key).ToList())
            {
                sent.Remove(stale);
            }

            sent[(kind, userId)] = now;

            return true;
        }
    }
}
