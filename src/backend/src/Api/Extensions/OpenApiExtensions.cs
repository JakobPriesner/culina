using Api.Infrastructure;
using Microsoft.OpenApi;

namespace Api.Extensions;

/// <summary>
/// The OpenAPI document, which is the contract the frontend client is
/// generated from.
/// </summary>
/// <remarks>
/// The document is exported to <c>openapi/Api.json</c> at build time and
/// committed, so the frontend generates from a file rather than from a running
/// server and CI can fail when the committed client no longer matches.
/// </remarks>
internal static class OpenApiExtensions
{
    internal static IServiceCollection AddCulinaOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Culina API",
                    Version = "v1",
                    Description =
                        "Culina's HTTP API. Every failure is an RFC 9457 problem document with a "
                        + "machine-readable `code` and a `requestId`; clients branch on `code`, "
                        + "never on a message."
                };

                document.Servers = [new OpenApiServer { Url = ApiPaths.V1 }];

                return Task.CompletedTask;
            });
        });
    }
}
