using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.Connect.V1;

/// <summary>Connects another app's recipe library.</summary>
/// <remarks>
/// Rate limited with the same policy the pasted-link import uses, because it is
/// the same risk: the server opens a connection to an address somebody else
/// chose. Unlike that one, this also carries a credential — which is why the
/// token is write-only and appears in no response on this API.
/// </remarks>
internal sealed class ConnectRecipeSourceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-sources", async (
                ConnectSourceRequest request,
                HttpContext context,
                ICommandHandler<ConnectSourceCommand, SourceSummary> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ConnectSourceCommand(context.CurrentUser().UserId, request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created(
                        $"{ApiPaths.V1}/recipe-sources/{created.SourceId}",
                        created),
                    CustomResults.Problem);
            })
            .WithName("connectRecipeSourceV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Connect another app's recipe library")
            .WithDescription(
                "Remembers where another recipe app is and the token to read it with, so bringing "
                + "recipes over is something you can come back to rather than do once. Nothing is "
                + "imported here.\n\n"
                + "Send either `token`, or `username` and `password` — one or the other, never "
                + "both. A name and password are traded with that app for a token and then "
                + "dropped: the password is not stored, not logged and not returned, and what is "
                + "kept is the same token you would have made by hand. The token path stays "
                + "because an instance whose accounts are all single sign-on has no password to "
                + "give.\n\n"
                + "The address and token are tried against that app before anything is stored, so "
                + "a wrong one is reported while the form is still open. `address` is reduced to "
                + "its scheme, host and port: a path is dropped rather than prefixed onto every "
                + "later request.\n\n"
                + "The token is write-only. It is never returned by this or any other endpoint.\n\n"
                + "Rate limited with the import policy, which matters more here than elsewhere: "
                + "a signed-in caller can make this server try a password against a host they "
                + "chose.\n\n"
                + "Only addresses on the public internet, unless the operator has set "
                + "`Import__AllowPrivateSourceAddresses` — a self-hosted recipe app is very often "
                + "on the same network as this one, and that is the operator's decision to make.")
            .RequireRateLimiting(RateLimitExtensions.Import)
            .Produces<SourceSummary>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();
    }
}
