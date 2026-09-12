using Application.Sessions.SignIn;
using Request = Contracts.Sessions.SignIn.Request;

namespace Api.Endpoints.Sessions.SignIn.V1;

/// <summary>Turns the request body and the connection into the command.</summary>
internal static class SignInRequestExtensions
{
    internal static SignInCommand ToCommand(this Request request, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new SignInCommand(
            request.Email,
            request.Password,
            // Correct only because forwarded headers ran first and trust only
            // the configured proxies.
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());
    }
}
