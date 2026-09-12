using Application.Abstractions.Messaging;
using Application.CookSessions;
using Application.Households.ChangeMemberRole;
using Application.Households.Create;
using Application.Households.CreateInvitation;
using Application.Households.Delete;
using Application.Households.GetAll;
using Application.Households.GetById;
using Application.Households.GetInvitations;
using Application.Households.GetMembers;
using Application.Households.RedeemInvitation;
using Application.Households.RemoveMember;
using Application.Households.Rename;
using Application.Households.RevokeInvitation;
using Application.Recipes.Create;
using Application.Recipes.Delete;
using Application.Recipes.GetAll;
using Application.Recipes.GetById;
using Application.Recipes.GetCookLog;
using Application.Recipes.GetImage;
using Application.Recipes.GetNotes;
using Application.Recipes.RecordCooked;
using Application.Recipes.RemoveImage;
using Application.Recipes.SaveNotes;
using Application.Recipes.SetImage;
using Application.Recipes.UndoCooked;
using Application.Recipes.Update;
using Application.Registration.GetPolicy;
using Application.Sessions.GetAll;
using Application.Sessions.Revoke;
using Application.Sessions.SignIn;
using Application.Settings.GetRegistration;
using Application.Settings.UpdateRegistration;
using Application.Shopping;
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
            .AddScoped<RegistrationDependencies>()
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
            .AddScoped<ICommandHandler<RemoveMemberCommand>, RemoveMemberCommandHandler>()
            .AddScoped<ICommandHandler<CreateInvitationCommand,
                Contracts.Households.CreateInvitation.Response>, CreateInvitationCommandHandler>()
            .AddScoped<IQueryHandler<GetInvitationsQuery, Contracts.Households.GetInvitations.Response>,
                GetInvitationsQueryHandler>()
            .AddScoped<ICommandHandler<RevokeInvitationCommand>, RevokeInvitationCommandHandler>()
            .AddScoped<ICommandHandler<RedeemInvitationCommand,
                Contracts.Households.RedeemInvitation.Response>, RedeemInvitationCommandHandler>()

            // Instance settings
            .AddScoped<IQueryHandler<GetRegistrationSettingsQuery,
                Contracts.Settings.GetRegistration.Response>, GetRegistrationSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UndoCookedCommand>, UndoCookedCommandHandler>()
            .AddScoped<IQueryHandler<GetShoppingListQuery,
                Contracts.Shopping.Response>, GetShoppingListQueryHandler>()
            .AddScoped<ICommandHandler<AddShoppingItemCommand,
                Contracts.Shopping.Response>, AddShoppingItemCommandHandler>()
            .AddScoped<ICommandHandler<AddRecipeToListCommand,
                Contracts.Shopping.Response>, AddRecipeToListCommandHandler>()
            .AddScoped<ICommandHandler<UpdateShoppingItemCommand,
                Contracts.Shopping.Response>, UpdateShoppingItemCommandHandler>()
            .AddScoped<ICommandHandler<RemoveShoppingItemsCommand,
                Contracts.Shopping.Response>, RemoveShoppingItemsCommandHandler>()
            .AddScoped<ICommandHandler<StartCookSessionCommand,
                Contracts.CookSessions.Response>, StartCookSessionCommandHandler>()
            .AddScoped<ICommandHandler<UpdateCookSessionCommand,
                Contracts.CookSessions.Response>, UpdateCookSessionCommandHandler>()
            .AddScoped<ICommandHandler<EndCookSessionCommand>, EndCookSessionCommandHandler>()
            .AddScoped<IQueryHandler<GetCurrentCookSessionQuery,
                Contracts.CookSessions.Response>, GetCurrentCookSessionQueryHandler>()
            .AddScoped<IQueryHandler<GetRegistrationPolicyQuery,
                Contracts.Registration.GetPolicy.Response>, GetRegistrationPolicyQueryHandler>()
            .AddScoped<ICommandHandler<UpdateRegistrationSettingsCommand,
                Contracts.Settings.UpdateRegistration.Response>,
                UpdateRegistrationSettingsCommandHandler>()

            // Recipes
            .AddScoped<ICommandHandler<CreateRecipeCommand, Contracts.Recipes.RecipeDetail>,
                CreateRecipeCommandHandler>()
            .AddScoped<IQueryHandler<GetRecipesQuery, Contracts.Recipes.GetAll.Response>,
                GetRecipesQueryHandler>()
            .AddScoped<IQueryHandler<GetRecipeQuery, Contracts.Recipes.RecipeDetail>,
                GetRecipeQueryHandler>()
            .AddScoped<ICommandHandler<UpdateRecipeCommand, Contracts.Recipes.RecipeDetail>,
                UpdateRecipeCommandHandler>()
            .AddScoped<ICommandHandler<DeleteRecipeCommand>, DeleteRecipeCommandHandler>()
            .AddScoped<IQueryHandler<GetNotesQuery, Contracts.Recipes.GetNotes.Response>,
                GetNotesQueryHandler>()
            .AddScoped<ICommandHandler<SaveNotesCommand, Contracts.Recipes.GetNotes.Response>,
                SaveNotesCommandHandler>()
            .AddScoped<IQueryHandler<GetCookLogQuery, Contracts.Recipes.GetCookLog.Response>,
                GetCookLogQueryHandler>()
            .AddScoped<ICommandHandler<RecordCookedCommand, Contracts.Recipes.RecordCooked.Response>,
                RecordCookedCommandHandler>()
            .AddScoped<ICommandHandler<SetRecipeImageCommand, Contracts.Recipes.RecipeDetail>,
                SetRecipeImageCommandHandler>()
            .AddScoped<ICommandHandler<RemoveRecipeImageCommand>, RemoveRecipeImageCommandHandler>()
            .AddScoped<IQueryHandler<GetRecipeImageQuery, ImageDelivery>, GetRecipeImageQueryHandler>();
    }
}
