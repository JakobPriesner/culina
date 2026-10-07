using Api.Endpoints.Invitations.GetByCode.V1;
using Api.Endpoints.Invitations.Redeem.V1;

namespace Api.Endpoints.Invitations;

/// <summary>The invitations endpoints. Redemption is its own resource: the redeemer has only the code, not the household id.</summary>
internal static class InvitationsEndpoints
{
    internal static IServiceCollection AddInvitationsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetInvitationEndpoint>()
            .AddSingleton<IEndpoint, RedeemInvitationEndpoint>();
}
