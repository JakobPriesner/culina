using Api.Endpoints.Invitations.Redeem.V1;

namespace Api.Endpoints.Invitations;

/// <summary>
/// The invitations domain's endpoints.
/// </summary>
/// <remarks>
/// Redemption is its own resource rather than a household sub-resource, because
/// the person redeeming does not know the household id — the code is all they
/// have.
/// </remarks>
internal static class InvitationsEndpoints
{
    internal static IServiceCollection AddInvitationsEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, RedeemInvitationEndpoint>();
}
