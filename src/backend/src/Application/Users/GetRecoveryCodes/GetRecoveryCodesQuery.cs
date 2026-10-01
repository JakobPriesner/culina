using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Users.GetRecoveryCodes.Response;

namespace Application.Users.GetRecoveryCodes;

/// <summary>How many of the signed-in user's recovery codes are left.</summary>
/// <param name="UserId">Who is asking.</param>
public sealed record GetRecoveryCodesQuery(Guid UserId);

internal sealed class GetRecoveryCodesQueryHandler(IRecoveryCodeRepository recoveryCodes)
    : IQueryHandler<GetRecoveryCodesQuery, Response>
{
    public async Task<Result<Response>> Handle(GetRecoveryCodesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Users.GetRecoveryCodes");

        var saved = await recoveryCodes.SavedAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(new Response
        {
            Remaining = saved.Remaining,
            CreatedAt = saved.CreatedAt
        }));
    }
}
