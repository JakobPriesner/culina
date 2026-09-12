using Application.Abstractions.Messaging;
using Application.Households.ChangeMemberRole;
using Application.Households.Create;
using Application.Households.Delete;
using Application.Households.GetAll;
using Application.Households.GetById;
using Application.Households.GetMembers;
using Application.Households.RemoveMember;
using Application.Households.Rename;
using Application.Sessions.GetAll;
using Application.Sessions.Revoke;
using Application.Sessions.SignIn;
using Application.Users.GetCurrent;
using Application.Users.GetPreferences;
using Application.Users.Register;
using Application.Users.UpdateCurrent;
using Application.Users.UpdatePreferences;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>
/// Registers every command and query handler, explicitly, one line each.
/// </summary>
/// <remarks>
/// <para>
/// No scanning. A handler that is deleted, renamed or never registered must
/// fail the build rather than surface as a 500 on the one route nobody tested;
/// an architecture test asserts that every handler in this assembly appears
/// here.
/// </para>
/// <para>
/// Handlers are scoped: they hold a unit of work and repositories, which belong
/// to one request.
/// </para>
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Adds the use cases.</summary>
    /// <param name="services">The container to register into.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Handlers are grouped by domain, in the same order as the folders.
        // Nothing depends on the order itself.
        return services
            // Users
            .AddScoped<ICommandHandler<RegisterUserCommand, Contracts.Users.Register.Response>,
                RegisterUserCommandHandler>()
            .AddScoped<IQueryHandler<GetCurrentUserQuery, Contracts.Users.GetCurrent.Response>,
                GetCurrentUserQueryHandler>()
            .AddScoped<ICommandHandler<UpdateCurrentUserCommand, Contracts.Users.UpdateCurrent.Response>,
                UpdateCurrentUserCommandHandler>()
            .AddScoped<IQueryHandler<GetPreferencesQuery, Contracts.Users.GetPreferences.Response>,
                GetPreferencesQueryHandler>()
            .AddScoped<ICommandHandler<UpdatePreferencesCommand, Contracts.Users.UpdatePreferences.Response>,
                UpdatePreferencesCommandHandler>()

            // Sessions
            .AddScoped<ICommandHandler<SignInCommand, SignInOutcome>, SignInCommandHandler>()
            .AddScoped<ICommandHandler<RevokeSessionCommand>, RevokeSessionCommandHandler>()
            .AddScoped<IQueryHandler<GetSessionsQuery, Contracts.Sessions.GetAll.Response>,
                GetSessionsQueryHandler>()
            .AddScoped<SignInDependencies>()

            // Households
            .AddScoped<IQueryHandler<GetHouseholdsQuery, Contracts.Households.GetAll.Response>,
                GetHouseholdsQueryHandler>()
            .AddScoped<ICommandHandler<CreateHouseholdCommand, Contracts.Households.Create.Response>,
                CreateHouseholdCommandHandler>()
            .AddScoped<IQueryHandler<GetHouseholdQuery, Contracts.Households.GetById.Response>,
                GetHouseholdQueryHandler>()
            .AddScoped<ICommandHandler<RenameHouseholdCommand, Contracts.Households.Rename.Response>,
                RenameHouseholdCommandHandler>()
            .AddScoped<ICommandHandler<DeleteHouseholdCommand>, DeleteHouseholdCommandHandler>()
            .AddScoped<IQueryHandler<GetMembersQuery, Contracts.Households.GetMembers.Response>,
                GetMembersQueryHandler>()
            .AddScoped<ICommandHandler<ChangeMemberRoleCommand,
                Contracts.Households.ChangeMemberRole.Response>, ChangeMemberRoleCommandHandler>()
            .AddScoped<ICommandHandler<RemoveMemberCommand>, RemoveMemberCommandHandler>();
    }
}
