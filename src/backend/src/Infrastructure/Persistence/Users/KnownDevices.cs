using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;

namespace Infrastructure.Persistence.Users;

/// <summary>Stores the kinds of device each person signed in from, hashed.</summary>
internal sealed class KnownDevices(DbExecutor executor) : IKnownDevices
{
    public async Task<bool> RememberAsync(Guid userId, string device, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        // One statement: the count is read from the snapshot before the insert, so the first device an account
        // ever uses is not news, and two sign-ins at once from a new device report it once.
        return await executor.ExecuteScalarAsync<bool>(
            """
            with earlier as (
                select count(*) as devices from known_devices where user_id = @userId
            ), remembered as (
                insert into known_devices (user_id, device_hash, first_seen_at, last_seen_at)
                values (@userId, @deviceHash, @now, @now)
                on conflict (user_id, device_hash) do update set last_seen_at = excluded.last_seen_at
                returning (xmax = 0) as inserted
            )
            select coalesce((select inserted from remembered), false) and (select devices from earlier) > 0;
            """,
            new { userId, deviceHash = SHA256.HashData(Encoding.UTF8.GetBytes(device)), now },
            cancellationToken).ConfigureAwait(false);
    }
}
