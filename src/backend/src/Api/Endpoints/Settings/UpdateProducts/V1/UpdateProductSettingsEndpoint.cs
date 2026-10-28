using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.UpdateProducts;
using Request = Contracts.Settings.UpdateProducts.Request;
using Response = Contracts.Settings.GetProducts.Response;

namespace Api.Endpoints.Settings.UpdateProducts.V1;

/// <summary>Switches the Open Food Facts lookup on or off.</summary>
internal sealed class UpdateProductSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/products", async (
                Request request,
                ICommandHandler<UpdateProductSettingsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new UpdateProductSettingsCommand(request.OpenFoodFacts, request.Contact), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateProductSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change the product lookup settings")
            .WithDescription(
                "Instance administrator only. On by default: a barcode no household named, and a name search a "
                + "person asks for, are sent from this server to openfoodfacts.org, with `contact` (or Culina's "
                + "project page when there is none) in the User-Agent. Off, nothing leaves the server. Takes effect "
                + "at once, with no restart.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
