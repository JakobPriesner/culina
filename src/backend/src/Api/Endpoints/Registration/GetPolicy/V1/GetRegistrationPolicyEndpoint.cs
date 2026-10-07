using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Registration.GetPolicy;
using Response = Contracts.Registration.GetPolicy.Response;

namespace Api.Endpoints.Registration.GetPolicy.V1;

/// <summary>
/// Tells a sign-up form what this instance allows. Anonymous, with only the global per-address ceiling:
/// the registration limit would be spent by page visits and link hovers.
/// </summary>
internal sealed class GetRegistrationPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/registration/policy", async (
                IQueryHandler<GetRegistrationPolicyQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetRegistrationPolicyQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRegistrationPolicyV1")
            .WithTags(Tags.Registration)
            .WithSummary("Read what a new account is allowed to do")
            .WithDescription(
                "Public: a sign-up form has to know which fields to ask for before anyone has an account.")
            .Produces<Response>()
            .AllowAnonymous();
    }
}
