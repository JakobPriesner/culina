using System.Text.Json.Serialization.Metadata;
using Api.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Extensions;

/// <summary>The OpenAPI document the frontend client is generated from, exported to <c>openapi/Api.json</c> and committed.</summary>
internal static class OpenApiExtensions
{
    private const string ContractsPrefix = "Contracts.";

    internal static IServiceCollection AddCulinaOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddOpenApi("v1", options =>
        {
            // Every operation has its own Request and Response, so names collide; the namespace distinguishes them.
            options.CreateSchemaReferenceId = type => SchemaName(type)
                ?? OpenApiOptions.CreateDefaultSchemaReferenceId(type);

            // Query parameters are declared once as endpoint metadata, read by the guard and here.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var allowed = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<AllowedQueryParameters>()
                    .FirstOrDefault();

                if (allowed is not null)
                {
                    OpenApiContractFixes.DescribeQueryParameters(operation, allowed);
                }

                return Task.CompletedTask;
            });

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

                // No `servers` entry: paths already carry /api/v1 and the client is same-origin.
                document.Servers = [];

                OpenApiContractFixes.CollapseStringOrIntegerUnions(document);
                OpenApiContractFixes.PublishVocabularies(document);
                OpenApiContractFixes.DescribeProblemExtensions(document);

                return Task.CompletedTask;
            });
        });
    }

    // Contracts.Users.Register.Response becomes UsersRegisterResponse.
    // Null outside Contracts, so the framework's rule applies and primitives get no schema entries.
    private static string? SchemaName(JsonTypeInfo type)
    {
        if (type.Type.Namespace is not { } ns || !ns.StartsWith(ContractsPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return string.Concat(ns[ContractsPrefix.Length..].Split('.')) + type.Type.Name;
    }
}
