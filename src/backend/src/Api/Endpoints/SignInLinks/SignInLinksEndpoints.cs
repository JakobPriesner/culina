using Api.Endpoints.SignInLinks.Create.V1;
using Api.Endpoints.SignInLinks.Redeem.V1;

namespace Api.Endpoints.SignInLinks;

/// <summary>The sign-in links domain's endpoints, registered explicitly.</summary>
internal static class SignInLinksEndpoints
{
    internal static IServiceCollection AddSignInLinksEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, CreateSignInLinkEndpoint>()
            .AddSingleton<IEndpoint, RedeemSignInLinkEndpoint>();
}
