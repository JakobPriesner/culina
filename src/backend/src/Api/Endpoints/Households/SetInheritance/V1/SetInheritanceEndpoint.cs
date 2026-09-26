using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.SetInheritance;
using Request = Contracts.Households.SetInheritance.Request;
using Response = Contracts.Households.SetInheritance.Response;

namespace Api.Endpoints.Households.SetInheritance.V1;

/// <summary>Chooses which household's recipes a household sees.</summary>
internal sealed class SetInheritanceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/inheritance", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<SetInheritanceCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new SetInheritanceCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.HouseholdId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    household => ETag.Ok(context, household, household.Version),
                    CustomResults.Problem);
            })
            .WithName("setHouseholdInheritanceV1")
            .WithTags(Tags.Households)
            .WithSummary("Choose which household's recipes this one inherits")
            .WithDescription(
                "Owners only, and only from a household you are in. The household then sees every "
                + "recipe the other one sees — its own and whatever it inherits in turn — without "
                + "being able to change them. Send null to inherit nothing.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .RequireAuthorization();
    }
}
