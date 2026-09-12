using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetRegistration.Response;

namespace Application.Settings.GetRegistration;

/// <summary>Reads the instance's registration policy.</summary>
public sealed record GetRegistrationSettingsQuery;

internal sealed class GetRegistrationSettingsQueryHandler(RegistrationSettings settings)
    : IQueryHandler<GetRegistrationSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(
        GetRegistrationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetRegistration");

        // Read straight from the singleton: it is the live value every other
        // handler sees, so there is nothing to fetch and nothing that could be
        // stale.
        return Task.FromResult(tracked.Record(Result<Response>.Success(new Response
        {
            OpenRegistration = settings.OpenRegistration,
            RequireInvitation = settings.RequireInvitation,
            MaxUsers = settings.MaxUsers
        })));
    }
}
