using System.Text.Json.Serialization.Metadata;
using Api.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Extensions;

/// <summary>
/// The OpenAPI document, which is the contract the frontend client is
/// generated from.
/// </summary>
/// <remarks>
/// The document is exported to <c>openapi/Api.json</c> and committed, so the
/// frontend generates from a file rather than from a running server and CI can
/// fail when the committed client no longer matches.
/// </remarks>
internal static class OpenApiExtensions
{
    private const string ContractsPrefix = "Contracts.";

    internal static IServiceCollection AddCulinaOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddOpenApi("v1", options =>
        {
            // Every operation owns its own Request and Response, so the type
            // names alone collide. The namespace is what distinguishes them,
            // and it is also how the operations are organised on disk.
            options.CreateSchemaReferenceId = type => SchemaName(type)
                ?? OpenApiOptions.CreateDefaultSchemaReferenceId(type);

            // Query parameters are declared once, as endpoint metadata, and
            // read twice: by the guard that rejects anything else, and here.
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

                // No `servers` entry: the paths already carry /api/v1 and the
                // app is always served from the same origin as its client, so
                // a base URL here would only double the prefix.
                document.Servers = [];

                OpenApiContractFixes.CollapseStringOrIntegerUnions(document);
                OpenApiContractFixes.PublishVocabularies(document);
                OpenApiContractFixes.DescribeProblemExtensions(document);

                return Task.CompletedTask;
            });
        });
    }

    /// <summary>
    /// <c>Contracts.Users.Register.Response</c> becomes
    /// <c>UsersRegisterResponse</c>: unique, stable, and readable in the
    /// generated client.
    /// </summary>
    /// <returns>
    /// Null for anything outside <c>Contracts</c>, so the framework's own rule
    /// applies — overriding it for every type would give primitives schema
    /// entries they should not have.
    /// </returns>
    private static string? SchemaName(JsonTypeInfo type)
    {
        if (type.Type.Namespace is not { } ns || !ns.StartsWith(ContractsPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return string.Concat(ns[ContractsPrefix.Length..].Split('.')) + type.Type.Name;
    }
}
