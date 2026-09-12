using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Registration.GetPolicy.Response;

namespace Application.Registration.GetPolicy;

/// <summary>Reads what a prospective account is allowed to do.</summary>
public sealed record GetRegistrationPolicyQuery;

internal sealed class GetRegistrationPolicyQueryHandler(
    RegistrationSettings settings,
    IUserRepository users)
    : IQueryHandler<GetRegistrationPolicyQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetRegistrationPolicyQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Registration.GetPolicy");

        var existing = await users.CountAsync(cancellationToken).ConfigureAwait(false);

        // The policy comes straight from the singleton, which is the live value
        // every other handler sees. Only the account count needs the database.
        return tracked.Record(Result<Response>.Success(new Response
        {
            OpenRegistration = settings.OpenRegistration,
            RequireInvitation = settings.RequireInvitation,
            HasAccounts = existing > 0
        }));
    }
}
