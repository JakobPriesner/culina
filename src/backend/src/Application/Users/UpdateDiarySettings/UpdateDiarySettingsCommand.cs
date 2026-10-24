using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Diary;
using Domain.Shared;
using Response = Contracts.Users.UpdateDiarySettings.Response;

namespace Application.Users.UpdateDiarySettings;

/// <summary>Changes a person's diary settings; switching off may delete every entry too.</summary>
/// <param name="UserId">Whose.</param>
/// <param name="Enabled">On or off.</param>
/// <param name="ConsentVersion">The wording agreed to when switching on.</param>
/// <param name="KcalTarget">Their kcal target, or null.</param>
/// <param name="ProteinTarget">Their protein target, or null.</param>
/// <param name="Headline"><c>eaten</c> or <c>remaining</c>.</param>
/// <param name="EraseEntries">When switching off, delete every entry too.</param>
public sealed record UpdateDiarySettingsCommand(
    Guid UserId,
    bool Enabled,
    int? ConsentVersion,
    int? KcalTarget,
    int? ProteinTarget,
    string? Headline,
    bool EraseEntries);

internal sealed class UpdateDiarySettingsCommandHandler(
    IDiaryRepository diary,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<UpdateDiarySettingsCommand, Response>
{
    public async Task<Result<Response>> Handle(UpdateDiarySettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.UpdateDiarySettings");

        var result = await unitOfWork.InTransactionAsync(
            async token =>
            {
                var current = await diary.SettingsAsync(command.UserId, token).ConfigureAwait(false);
                var now = time.GetUtcNow();
                var changed = DiaryMappings.ToHeadline(command.Headline) is { } headline
                    ? current.Change(
                        command.Enabled,
                        command.ConsentVersion,
                        command.KcalTarget,
                        command.ProteinTarget,
                        headline,
                        now)
                    : DiaryErrors.InvalidHeadline;

                return await changed.Match(
                    async settings =>
                    {
                        await diary.SaveSettingsAsync(settings, now, token).ConfigureAwait(false);

                        // Switching off withdraws the agreement; deleting the entries with it is the person's choice.
                        if (!settings.Enabled && command.EraseEntries)
                        {
                            await diary.RemoveAllAsync(command.UserId, token).ConfigureAwait(false);
                        }

                        return Result<Response>.Success(settings.ToUpdateResponse());
                    },
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>Maps diary settings onto the shape this operation returns.</summary>
internal static class UpdateDiarySettingsMappings
{
    internal static Response ToUpdateResponse(this DiarySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new Response
        {
            Enabled = settings.Enabled,
            ConsentedAt = settings.ConsentedAt,
            ConsentVersion = settings.ConsentVersion,
            CurrentConsentVersion = DiarySettings.CurrentConsentVersion,
            KcalTarget = settings.KcalTarget,
            ProteinTarget = settings.ProteinTarget,
            Headline = DiaryMappings.HeadlineCode(settings.Headline),
            Version = settings.Version
        };
    }
}
