using Application.Users.Register;
using Request = Contracts.Users.Register.Request;

namespace Api.Endpoints.Users.Register.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
/// <remarks>
/// Trivial by design. It exists so that adding a field is one edit in one
/// predictable place and the endpoint body never grows past a single handler
/// call.
/// </remarks>
internal static class RegisterUserRequestExtensions
{
    internal static RegisterUserCommand ToCommand(this Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RegisterUserCommand(
            request.Email,
            request.DisplayName,
            request.Password,
            request.HouseholdName,
            request.InvitationCode);
    }
}
