using Application.Users.UpdateCurrent;
using Request = Contracts.Users.UpdateCurrent.Request;

namespace Api.Endpoints.Users.UpdateCurrent.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class UpdateCurrentUserRequestExtensions
{
    internal static UpdateCurrentUserCommand ToCommand(this Request request, Guid userId, long version)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateCurrentUserCommand(userId, request.DisplayName, version);
    }
}
