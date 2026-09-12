using Domain.Users;
using Response = Contracts.Sessions.SignIn.Response;

namespace Application.Sessions.SignIn;

/// <summary>Maps a user onto the shape this operation returns.</summary>
internal static class SessionMappings
{
    internal static Response ToSignInResponse(this User user, bool isAdmin, string csrfToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new Response
        {
            UserId = user.Id,
            DisplayName = user.DisplayName.Value,
            Email = user.Email.Value,
            IsAdmin = isAdmin,
            CsrfToken = csrfToken
        };
    }
}
