using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads an app's Client ID Metadata Document: the JSON at the https address it uses as its <c>client_id</c>.</summary>
public interface IClientMetadataDocuments
{
    /// <summary>Fetches and parses the document, from a public address only.</summary>
    /// <returns><c>oauth.invalid_client</c> when it cannot be fetched or is not a JSON object.</returns>
    Task<Result<ClientMetadataDocument>> FetchAsync(Uri address, CancellationToken cancellationToken);
}

/// <summary>What a Client ID Metadata Document says about its app, unchecked.</summary>
/// <param name="ClientId">The <c>client_id</c> it claims, which must be its own address.</param>
/// <param name="ClientName">The app's name.</param>
/// <param name="RedirectUris">Its return addresses.</param>
public sealed record ClientMetadataDocument(string? ClientId, string? ClientName, IReadOnlyList<string> RedirectUris);
