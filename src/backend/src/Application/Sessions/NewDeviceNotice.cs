using Application.Abstractions;
using Application.Mail;
using Domain.Sessions;
using Domain.Users;

namespace Application.Sessions;

/// <summary>Remembers the device every sign-in came from, and mails the owner when it is one they have not used before.</summary>
/// <remarks>
/// Remembered even while mail is off, so switching mail on later does not greet everybody with a notice for each
/// device they already use. A device is its kind (<c>Firefox · macOS</c>), not one machine: a second Mac with
/// Firefox is no news, which keeps the mail rare enough to be read.
/// </remarks>
internal sealed class NewDeviceNotice(IKnownDevices devices, AccountMail mail)
{
    internal async Task RecordAsync(
        User user,
        SessionClient client,
        string? userAgent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var description = DeviceDescription.Of(userAgent, client);

        if (await devices.RememberAsync(user.Id, description, now, cancellationToken).ConfigureAwait(false))
        {
            await mail.TellNewDeviceAsync(user, description, now, cancellationToken).ConfigureAwait(false);
        }
    }
}
