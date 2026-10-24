using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetMail.Response;

namespace Application.Settings.GetMail;

/// <summary>Reads the mail account.</summary>
public sealed record GetMailSettingsQuery;

internal sealed class GetMailSettingsQueryHandler(MailSettings settings, SiteSettings site)
    : IQueryHandler<GetMailSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(GetMailSettingsQuery query, CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetMail");

        return Task.FromResult(tracked.Record(Result<Response>.Success(settings.ToResponse(site))));
    }
}
