using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.UpdateRegistration.Response;

namespace Application.Settings.UpdateRegistration;

/// <summary>Changes the instance's registration policy.</summary>
/// <param name="OpenRegistration">Whether anyone may create an account.</param>
/// <param name="RequireInvitation">Whether a code is required.</param>
/// <param name="MaxUsers">The account ceiling.</param>
public sealed record UpdateRegistrationSettingsCommand(
    bool OpenRegistration,
    bool RequireInvitation,
    int MaxUsers);

internal sealed class UpdateRegistrationSettingsCommandHandler(
    RegistrationSettings settings,
    ISettingsStore<RegistrationSettings> store)
    : ICommandHandler<UpdateRegistrationSettingsCommand, Response>
{
    private const int LargestReasonableInstance = 100_000;

    public async Task<Result<Response>> Handle(
        UpdateRegistrationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateRegistration");

        if (command.MaxUsers is < 1 or > LargestReasonableInstance)
        {
            return tracked.Record(Result<Response>.Failure(new FieldError(
                "maxUsers",
                SettingsErrors.InvalidValue.Code,
                $"The account limit must be between 1 and {LargestReasonableInstance}.")));
        }

        var updated = new RegistrationSettings
        {
            OpenRegistration = command.OpenRegistration,
            RequireInvitation = command.RequireInvitation,
            MaxUsers = command.MaxUsers
        };

        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        // Persist first, then mutate. A failed save must never leave the
        // process disagreeing with the database about who may register.
        var result = saved.Map(() =>
        {
            settings.CopyFrom(updated);

            return new Response
            {
                OpenRegistration = settings.OpenRegistration,
                RequireInvitation = settings.RequireInvitation,
                MaxUsers = settings.MaxUsers
            };
        });

        return tracked.Record(result);
    }
}
