using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Response = Contracts.Settings.GetMail.Response;

namespace Application.Settings.UpdateMail;

/// <summary>Changes the mail account.</summary>
/// <param name="Enabled">Whether mail is on.</param>
/// <param name="Host">The SMTP server.</param>
/// <param name="Port">The SMTP port.</param>
/// <param name="Security"><c>none</c>, <c>starttls</c> or <c>tls</c>.</param>
/// <param name="Username">Who to sign in as, or empty.</param>
/// <param name="Password">A new password, empty to clear it, or null to keep it (only while the server is unchanged).</param>
/// <param name="FromAddress">The sender's address.</param>
/// <param name="FromName">The sender's name.</param>
public sealed record UpdateMailSettingsCommand(
    bool Enabled,
    string Host,
    int Port,
    string Security,
    string Username,
    string? Password,
    string FromAddress,
    string FromName);

internal sealed class UpdateMailSettingsCommandHandler(
    MailSettings settings,
    SiteSettings site,
    ISettingsStore<MailSettings> store,
    [FromKeyedServices(MailSettings.PasswordProtection)] ISecretProtector protector)
    : ICommandHandler<UpdateMailSettingsCommand, Response>
{
    private static readonly string[] Securities = [MailSettings.SecurityNone, MailSettings.SecurityStartTls, MailSettings.SecurityTls];

    public async Task<Result<Response>> Handle(UpdateMailSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateMail");

        var updated = Prepare(command);

        var result = await Check(command, updated).Match(
            async () =>
            {
                var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

                // Persist first, then mutate: a failed save must not change the running process.
                return saved.Map(() =>
                {
                    settings.CopyFrom(updated);

                    return settings.ToResponse(site);
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private MailSettings Prepare(UpdateMailSettingsCommand command)
    {
        var host = command.Host.Trim();

        return new MailSettings
        {
            Enabled = command.Enabled,
            Host = host,
            Port = command.Port,
            Security = command.Security.Trim().ToLowerInvariant(),
            Username = command.Username.Trim(),
            ProtectedPassword = command.Password switch
            {
                null => settings.ProtectedPassword,
                var entered when entered.Length is 0 => string.Empty,
                // Not trimmed: a password may well end in a space.
                var entered => protector.Protect(entered)
            },
            FromAddress = command.FromAddress.Trim(),
            FromName = command.FromName.Trim()
        };
    }

    /// <summary>Everything wrong with the form, as field errors.</summary>
    private Result Check(UpdateMailSettingsCommand command, MailSettings updated)
    {
        List<Result> problems =
        [
            Length("host", updated.Host),
            Length("username", updated.Username),
            Length("fromName", updated.FromName),
            command.Password is { Length: > MailSettings.MaxLength } ? Field("password", MailErrors.InvalidValue) : Result.Success(),
            updated.Port is < 1 or > 65535 ? Field("port", MailErrors.InvalidValue) : Result.Success(),
            Securities.Contains(updated.Security) ? Result.Success() : Field("security", MailErrors.InvalidValue),
            updated.FromAddress.Length is 0 || Domain.Users.Email.Create(updated.FromAddress).Match(_ => true, _ => false)
                ? Result.Success()
                : Field("fromAddress", MailErrors.InvalidValue),
            // Sent to wherever the server now is, so a kept password must have been entered for that server.
            command.Password is null && settings.HasPassword && settings.Host != updated.Host
                ? Field("password", MailErrors.PasswordRequired)
                : Result.Success()
        ];

        if (updated.Enabled && !updated.IsComplete)
        {
            problems.Add(MailErrors.Incomplete);
        }
        else if (updated.Enabled && site.Url is null)
        {
            problems.Add(MailErrors.SiteUrlRequired);
        }

        return Result.Combine(problems);
    }

    private static Result Length(string field, string value) =>
        value.Length > MailSettings.MaxLength ? Field(field, MailErrors.InvalidValue) : Result.Success();

    private static Result Field(string field, Error error) =>
        new FieldError(field, error.Code, error.Description);
}
