using System.Text.Json.Serialization;

namespace Api.Agents.Authorization;

/// <summary>The client metadata an app registers itself with (RFC 7591, 2); what Culina does not use is ignored.</summary>
internal sealed record ClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public IReadOnlyList<string>? RedirectUris { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("grant_types")]
    public IReadOnlyList<string>? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public IReadOnlyList<string>? ResponseTypes { get; init; }
}
