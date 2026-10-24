using Application.Abstractions;
using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.AgentAuthorizations;

/// <summary>Finds the app behind a <c>client_id</c>: from its metadata document when the id is an https address, else among self-registered apps.</summary>
/// <remarks>
/// A metadata document (the MCP authorization spec's preferred way) is read afresh for every request, so a changed
/// list of return addresses applies at once and nothing an app said yesterday is trusted today.
/// </remarks>
internal sealed class ClientRegistrations(IAgentClientRepository clients, IClientMetadataDocuments documents)
{
    private const int MaxClientIdLength = 2048;

    internal async Task<Result<ClientRegistration>> FindAsync(string clientId, CancellationToken cancellationToken)
    {
        if (!clientId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await clients.FindAsync(clientId, cancellationToken).ConfigureAwait(false);
        }

        if (MetadataAddress(clientId) is not { } address)
        {
            return AgentAuthorizationErrors.InvalidClient(
                "a client_id that is a URL must be an https address with a path, and nothing else");
        }

        var document = await documents.FetchAsync(address, cancellationToken).ConfigureAwait(false);

        return document.Bind(found => Registration(clientId, address, found));
    }

    /// <summary>The address of a Client ID Metadata Document, as its draft requires one to look.</summary>
    private static Uri? MetadataAddress(string clientId) =>
        clientId.Length <= MaxClientIdLength
        && Uri.TryCreate(clientId, UriKind.Absolute, out var address)
        && address.Scheme == Uri.UriSchemeHttps
        && address.AbsolutePath.Length > 1
        && address.UserInfo.Length == 0
        && address.Fragment.Length == 0
        && !clientId.Contains('#', StringComparison.Ordinal)
        && !address.Segments.Any(segment => segment.TrimEnd('/') is "." or "..")
            ? address
            : null;

    private static Result<ClientRegistration> Registration(string clientId, Uri address, ClientMetadataDocument document)
    {
        // The document vouches for itself only at its own address: one that names another client_id is somebody
        // else's, served from here.
        if (!string.Equals(document.ClientId, clientId, StringComparison.Ordinal))
        {
            return AgentAuthorizationErrors.InvalidClient("its metadata document names a different client_id");
        }

        if (string.IsNullOrWhiteSpace(document.ClientName) || document.RedirectUris.Count == 0)
        {
            return AgentAuthorizationErrors.InvalidClient(
                "its metadata document must give a client_name and at least one redirect URI");
        }

        var client = new AgentClient(clientId, AgentClient.CleanName(document.ClientName, address.Host), address.Host);

        return new ClientRegistration(client, document.RedirectUris);
    }
}
