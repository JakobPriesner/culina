using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Diary;
using Domain.Shared;
using Response = Contracts.Users.GetDiarySettings.Response;

namespace Application.Users.GetDiarySettings;

/// <summary>Reads whether a person keeps a diary, and their targets.</summary>
/// <param name="UserId">Whose.</param>
public sealed record GetDiarySettingsQuery(Guid UserId);

internal sealed class GetDiarySettingsQueryHandler(IDiaryRepository diary)
    : IQueryHandler<GetDiarySettingsQuery, Response>
{
    public async Task<Result<Response>> Handle(GetDiarySettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Users.GetDiarySettings");

        var settings = await diary.SettingsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(settings.ToGetResponse()));
    }
}

/// <summary>Maps diary settings onto the shape this operation returns.</summary>
internal static class GetDiarySettingsMappings
{
    internal static Response ToGetResponse(this DiarySettings settings)
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
