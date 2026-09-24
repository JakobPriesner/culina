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

    /// <summary>
    /// The administrator — or, while nobody has an account, whoever is setting
    /// the instance up.
    /// </summary>
    /// <remarks>
    /// For the server and database settings, which the setup screen fills in
    /// before the first account exists. It opens nothing that was closed
    /// before: until there is an account, anyone may create the first one and
    /// become the administrator anyway.
    /// </remarks>
    internal const string OrSetupName = "culina.admin-or-setup";

    internal static AuthorizationBuilder AddAdminPolicy(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddPolicy(Name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminRequirement()));
    }

    /// <summary>
    /// No authenticated user required: during setup there is nobody who could
    /// be. Once there is, the requirement is the administrator's.
    /// </summary>
    internal static AuthorizationBuilder AddAdminOrSetupPolicy(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddPolicy(OrSetupName, policy => policy
            .AddRequirements(new AdminOrSetupRequirement()));
    }
}

/// <summary>Marks a policy as needing the instance administrator.</summary>
internal class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// The administrator's requirement, which setup also satisfies. A subclass so
/// the administrator's handler answers it without knowing it exists.
/// </summary>
internal sealed class AdminOrSetupRequirement : AdminRequirement;

/// <summary>Satisfies the setup requirement until somebody administers the instance.</summary>
/// <param name="setup">Says how far setup has got.</param>
internal sealed class SetupRequirementHandler(ISetupProgress setup)
    : AuthorizationHandler<AdminOrSetupRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOrSetupRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await setup.CurrentAsync(CancellationToken.None).ConfigureAwait(false) != SetupStage.Complete)
        {
            context.Succeed(requirement);
        }
    }
}

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
