using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetRecoveryCodes;
using Response = Contracts.Users.GetRecoveryCodes.Response;

namespace Api.Endpoints.Users.GetRecoveryCodes.V1;

/// <summary>Says how many of the signed-in user's recovery codes are left.</summary>
internal sealed class GetRecoveryCodesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/users/me/recovery-codes", async (
                HttpContext context,
                IQueryHandler<GetRecoveryCodesQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetRecoveryCodesQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRecoveryCodesV1")
            .WithTags(Tags.Users)
            .WithSummary("Count your recovery codes")
            .WithDescription("How many unused codes are left, and when they were made. Never the codes.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
