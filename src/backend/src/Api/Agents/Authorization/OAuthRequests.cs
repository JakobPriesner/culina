using Application.AgentAuthorizations.Redeem;
using Application.AgentAuthorizations.Register;
using Application.AgentAuthorizations.Start;
using Microsoft.Extensions.Primitives;

namespace Api.Agents.Authorization;

/// <summary>Turns the OAuth endpoints' query, form and JSON parameters into commands.</summary>
internal static class OAuthRequests
{
    internal static StartAgentAuthorizationCommand ToStartCommand(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IEnumerable<KeyValuePair<string, StringValues>> query = context.Request.Query;
        var repeated = new HashSet<string>(StringComparer.Ordinal);

        // Read one by one, each noting a repetition; scope is not read at all, since an agent session's reach is fixed.
        return new StartAgentAuthorizationCommand(
            OAuthResults.Single(query, "client_id", repeated),
            OAuthResults.Single(query, "redirect_uri", repeated),
            OAuthResults.Single(query, "response_type", repeated),
            OAuthResults.Single(query, "code_challenge", repeated),
            OAuthResults.Single(query, "code_challenge_method", repeated),
            OAuthResults.Single(query, "state", repeated),
            OAuthResults.Single(query, "resource", repeated),
            repeated,
            OAuthAddresses.Issuer(context),
            OAuthAddresses.McpResource(context));
    }

    internal static RedeemAuthorizationCodeCommand ToRedeemCommand(this IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var repeated = new HashSet<string>(StringComparer.Ordinal);

        return new RedeemAuthorizationCodeCommand(
            OAuthResults.Single(form, "grant_type", repeated),
            OAuthResults.Single(form, "code", repeated),
            OAuthResults.Single(form, "redirect_uri", repeated),
            OAuthResults.Single(form, "client_id", repeated),
            OAuthResults.Single(form, "code_verifier", repeated),
            OAuthResults.Single(form, "resource", repeated),
            repeated);
    }

    internal static RegisterAgentClientCommand ToCommand(this ClientRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RegisterAgentClientCommand(request.RedirectUris, request.ClientName, request.GrantTypes, request.ResponseTypes);
    }
}
