using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Sessions.SignIn;
using Request = Contracts.Sessions.SignIn.Request;

namespace Api.Endpoints.Sessions.SignIn.V1;

/// <summary>Turns the request body and the connection into the command.</summary>
internal static class SignInRequestExtensions
{
    internal static SignInCommand ToCommand(this Request request, HttpContext context, CookieSettings cookies)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new SignInCommand(
            request.Email,
            request.Password,
            context.ClientAddress(),
            context.Request.Headers.UserAgent.ToString(),
            SessionCookies.Read(context, cookies));
    }
}
