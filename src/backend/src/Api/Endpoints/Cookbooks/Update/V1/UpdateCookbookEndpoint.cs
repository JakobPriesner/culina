using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.Update.V1;

/// <summary>Renames a cookbook.</summary>
internal sealed class UpdateCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}", async (
                Guid cookbookId,
                UpdateCookbookRequest request,
                HttpContext context,
                ICommandHandler<UpdateCookbookCommand, CookbookDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                return await expected.Match(
                    async version =>
                    {
                        var result = await handler
                            .Handle(
                                new UpdateCookbookCommand(
                                    cookbookId,
                                    context.CurrentUser().UserId,
                                    version,
                                    request),
                                cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(
                            cookbook => ETag.Ok(context, cookbook, cookbook.Version),
                            CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("updateCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Rename a cookbook")
            .WithDescription(
                "The name and what it is for, together: they are edited in one form, and two "
                + "requests for one form is two ways for half of it to fail. `If-Match` is "
                + "required — missing is 428, stale is 412.")
            .Produces<CookbookDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
