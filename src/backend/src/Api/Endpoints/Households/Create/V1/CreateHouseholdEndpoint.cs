using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.Create;
using Request = Contracts.Households.Create.Request;
using Response = Contracts.Households.Create.Response;

namespace Api.Endpoints.Households.Create.V1;

/// <summary>Creates a household.</summary>
internal sealed class CreateHouseholdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households", async (
                Request request,
                HttpContext context,
                ICommandHandler<CreateHouseholdCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateHouseholdCommand(context.CurrentUser().UserId, request.Name),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created(
                        $"{ApiPaths.V1}/households/{created.HouseholdId}",
                        created),
                    CustomResults.Problem);
            })
            .WithName("createHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Create a household")
            .WithDescription("The caller becomes its first owner.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
