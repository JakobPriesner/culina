using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.AgentAuthorizations;
using Domain.Shared;
using Microsoft.Extensions.Primitives;

namespace Api.Agents.Authorization;

/// <summary>How the OAuth endpoints read their parameters and answer: the protocol's own shapes, not the API's problem documents.</summary>
internal static class OAuthResults
{
    /// <summary>OAuth's wire format: snake_case names, and an absent value left out rather than sent as null.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>A successful answer that no cache may keep, since it carries a credential or a fresh client (RFC 6749, 5.1).</summary>
    internal static IResult Ok<T>(HttpContext context, T value, int status = StatusCodes.Status200OK)
    {
        NoStore(context);

        return Results.Json(value, Json, statusCode: status);
    }

    /// <summary>An error as RFC 6749 (5.2) and RFC 7591 (3.2.2) write it; <c>invalid_client</c> is a 401.</summary>
    internal static IResult Error(HttpContext context, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        NoStore(context);

        var code = AgentAuthorizationErrors.OAuthCode(error);

        return Results.Json(
            new OAuthError(code, error.Description),
            Json,
            statusCode: code == "invalid_client" ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest);
    }

    /// <summary>The one value of a parameter, and the parameters given more than once (which OAuth forbids).</summary>
    internal static string? Single(IEnumerable<KeyValuePair<string, StringValues>> parameters, string name, ISet<string> repeated)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(repeated);

        foreach (var (key, values) in parameters)
        {
            if (!string.Equals(key, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (values.Count > 1)
            {
                repeated.Add(name);
            }

            // An empty value is the same as none (RFC 6749, 3.1).
            return values.Count == 1 && !string.IsNullOrEmpty(values[0]) ? values[0] : null;
        }

        return null;
    }

    private static void NoStore(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private sealed record OAuthError(string Error, string ErrorDescription);
}
