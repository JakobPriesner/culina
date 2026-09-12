using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.Rename;
using Domain.Shared;
using Request = Contracts.Households.Rename.Request;
using Response = Contracts.Households.Rename.Response;

namespace Api.Endpoints.Households.Rename.V1;

/// <summary>Renames a household.</summary>
internal sealed class RenameHouseholdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/households/{{householdId:guid}}", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<RenameHouseholdCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                var result = await expected.Match(
                    version => handler.Handle(
                        new RenameHouseholdCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.Name,
                            version),
                        cancellationToken),
                    error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

                return result.Match(
                    household => ETag.Ok(context, household, household.Version),
                    CustomResults.Problem);
            })
            .WithName("renameHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Rename a household")
            .WithDescription("Owners only. Requires If-Match with the version you last read.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
