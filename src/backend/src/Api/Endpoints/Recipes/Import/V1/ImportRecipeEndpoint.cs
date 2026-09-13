using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Import;
using Contracts.Recipes.Import;

namespace Api.Endpoints.Recipes.Import.V1;

/// <summary>Reads a recipe from a web page.</summary>
/// <remarks>
/// The one endpoint that makes the server open a connection somewhere a user
/// chose, which is why it is signed in, rate limited hard, and backed by a
/// fetcher that refuses every address that is not the open internet. See
/// <c>SafeWebPageFetcher</c>.
/// </remarks>
internal sealed class ImportRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-imports", async (
                Request request,
                HttpContext context,
                IQueryHandler<ImportRecipeQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ImportRecipeQuery(request.Url, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("importRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe from a web page")
            .WithDescription(
                "Reads the schema.org Recipe a site publishes for search engines, and falls back "
                + "to the page's words. Nothing is created: what comes back is a draft, shown for "
                + "correction before anything is saved.\n\n"
                + "Only ordinary public http and https pages. The address is resolved and checked "
                + "before every connection — including after each redirect — because the server "
                + "does the fetching, and an unguarded one would read the private network it sits "
                + "in. Refusals are deliberately vague about which address was refused and why.")
            .RequireRateLimiting(RateLimitExtensions.Import)
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();
    }
}
