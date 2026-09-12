using System.Globalization;
using Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authentication;

/// <summary>
/// Requires the account that administers this instance.
/// </summary>
/// <remarks>
/// Checked against the database on each request rather than carried as a claim.
/// A claim would be stale until the next sign-in, and "the stored row is the
/// truth" is the property the whole session design is built on — it would be
/// odd to abandon it for the one decision that gates instance-wide settings.
/// The cost is one indexed lookup on a handful of endpoints.
/// </remarks>
internal static class AdminPolicy
{
    internal const string Name = "culina.admin";

    internal static AuthorizationBuilder AddAdminPolicy(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddPolicy(Name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminRequirement()));
    }
}

/// <summary>Marks a policy as needing the instance administrator.</summary>
internal sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>Answers the administrator requirement from the users table.</summary>
/// <param name="users">Reads the admin flag.</param>
internal sealed class AdminRequirementHandler(IUserRepository users)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.FindFirst(CulinaClaims.UserId)?.Value is not { } raw
            || !Guid.TryParse(raw, CultureInfo.InvariantCulture, out var userId))
        {
            return;
        }

        if (await users.IsAdminAsync(userId, CancellationToken.None).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
