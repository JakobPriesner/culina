using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Sessions.SignIn;

namespace Api.Endpoints.SignInLinks;

/// <summary>Answers a sign-in from a mailed link, which always comes from a browser: the token becomes the cookie.</summary>
internal static class LinkSessionResults
{
    internal static IResult Created(HttpContext context, CookieSettings cookies, TimeProvider time, SignInOutcome outcome)
    {
        SessionCookies.Write(context, cookies, time.GetUtcNow(), outcome.SessionToken, outcome.Response.CsrfToken);

        return Results.Created($"{ApiPaths.V1}/sessions/current", outcome.Response);
    }
}
