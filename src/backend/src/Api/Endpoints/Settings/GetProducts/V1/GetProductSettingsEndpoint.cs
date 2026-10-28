using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetProducts;
using Response = Contracts.Settings.GetProducts.Response;

namespace Api.Endpoints.Settings.GetProducts.V1;

/// <summary>Reads whether barcodes are looked up at Open Food Facts.</summary>
internal sealed class GetProductSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/products", async (
                IQueryHandler<GetProductSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetProductSettingsQuery(), cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getProductSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Read the product lookup settings")
            .WithDescription("Instance administrator only.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
