using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Mail;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;

namespace Application.Settings.SendMailTest;

/// <summary>Sends a test mail to the administrator, now, through the saved account.</summary>
/// <param name="AdministratorId">Who asked, and where the mail goes.</param>
/// <remarks>Sent directly rather than queued: the point is to see what the server answers.</remarks>
public sealed record SendMailTestCommand(Guid AdministratorId);

internal sealed class SendMailTestCommandHandler(
    IUserRepository users,
    MailSettings settings,
    AccountMail mail,
    IMailTransport transport)
    : ICommandHandler<SendMailTestCommand>
{
    public async Task<Result> Handle(SendMailTestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.SendMailTest");

        if (!settings.IsOn)
        {
            return tracked.Record(Result.Failure(MailErrors.Off));
        }

        var found = await users.FindAsync(command.AdministratorId, cancellationToken).ConfigureAwait(false);

        var result = await found.Match(
            user => SendAsync(user, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> SendAsync(User user, CancellationToken cancellationToken) =>
        await transport
            .SendAsync(settings, await mail.TestAsync(user, cancellationToken).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
}
