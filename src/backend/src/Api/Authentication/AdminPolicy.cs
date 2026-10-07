using System.Globalization;
using Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authentication;

/// <summary>Requires the account that administers this instance.</summary>
/// <remarks>
/// Checked against the database on each request, not carried as a claim that would be stale until
/// the next sign-in: the stored row is the truth. Costs one indexed lookup.
/// </remarks>
internal static class AdminPolicy
{
    internal const string Name = "culina.admin";

    /// <summary>
    /// The administrator, or whoever is setting the instance up while nobody has an account.
    /// </summary>
    /// <remarks>
    /// For the settings the setup screen fills in before the first account exists; it opens
    /// nothing, as anyone may create the first account anyway.
    /// </remarks>
    internal const string OrSetupName = "culina.admin-or-setup";

    /// <summary>The request's abort token, so a caller who hung up costs no lookup.</summary>
    internal static CancellationToken RequestAborted(AuthorizationHandlerContext context) =>
        (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;

    internal static AuthorizationBuilder AddAdminPolicy(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddPolicy(Name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminRequirement()));
    }

    /// <summary>
    /// No authenticated user required during setup, when nobody could be; afterwards the
    /// administrator's requirement.
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
/// The administrator's requirement, which setup also satisfies; a subclass so the administrator's
/// handler answers it unaware.
/// </summary>
internal sealed class AdminOrSetupRequirement : AdminRequirement;

/// <summary>Satisfies the setup requirement until somebody administers the instance.</summary>
internal sealed class SetupRequirementHandler(ISetupProgress setup)
    : AuthorizationHandler<AdminOrSetupRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOrSetupRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await setup.CurrentAsync(AdminPolicy.RequestAborted(context)).ConfigureAwait(false) != SetupStage.Complete)
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Answers the administrator requirement from the users table.</summary>
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

        if (await users.IsAdminAsync(userId, AdminPolicy.RequestAborted(context)).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
