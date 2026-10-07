using Application.Users.Register;
using Request = Contracts.Users.Register.Request;

namespace Api.Endpoints.Users.Register.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
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
