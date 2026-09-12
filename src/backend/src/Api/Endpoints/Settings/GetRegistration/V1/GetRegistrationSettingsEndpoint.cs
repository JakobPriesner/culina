using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetRegistration;
using Response = Contracts.Settings.GetRegistration.Response;

namespace Api.Endpoints.Settings.GetRegistration.V1;

/// <summary>Reads the registration policy.</summary>
internal sealed class GetRegistrationSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/registration", async (
                IQueryHandler<GetRegistrationSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetRegistrationSettingsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRegistrationSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Read the registration policy")
            .WithDescription("Instance administrator only.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
