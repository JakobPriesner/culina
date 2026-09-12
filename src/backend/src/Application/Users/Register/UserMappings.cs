using Domain.Users;
using Response = Contracts.Users.Register.Response;

namespace Application.Users.Register;

/// <summary>Maps a user onto the shape this operation returns.</summary>
internal static class UserMappings
{
    internal static Response ToRegisterResponse(this User user, bool isAdmin, Guid? householdId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new Response
        {
            UserId = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName.Value,
            IsAdmin = isAdmin,
            HouseholdId = householdId
        };
    }
}
