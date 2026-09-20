using Application.Abstractions.Messaging;
using Application.Archive;
using Application.Assistance;
using Application.Cookbooks;
using Application.Cooking.CookPhoto;
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
using Application.Planning;
using Application.Recipes;
using Application.Recipes.Create;
using Application.Recipes.CreateShare;
using Application.Recipes.Delete;
using Application.Recipes.Drafts;
using Application.Recipes.DrawImage;
using Application.Recipes.GetAll;
using Application.Recipes.GetById;
using Application.Recipes.GetCookLog;
using Application.Recipes.GetImage;
using Application.Recipes.GetIngredients;
using Application.Recipes.GetNotes;
using Application.Recipes.GetShare;
using Application.Recipes.GetShared;
using Application.Recipes.GetSharedImage;
using Application.Recipes.GetTags;
using Application.Recipes.GetUnits;
using Application.Recipes.Import;
using Application.Recipes.RecordCooked;
using Application.Recipes.RemoveImage;
using Application.Recipes.RevokeShare;
using Application.Recipes.SaveNotes;
using Application.Recipes.SetImage;
using Application.Recipes.Sources;
using Application.Recipes.UndoCooked;
using Application.Recipes.Update;
using Application.Registration.GetPolicy;
using Application.Searches;
using Application.Sessions.GetAll;
using Application.Sessions.Revoke;
using Application.Sessions.SignIn;
using Application.Settings.GetAssistance;
using Application.Settings.GetAssistanceModels;
using Application.Settings.GetAssistanceUsage;
using Application.Settings.GetRegistration;
using Application.Settings.UpdateAssistance;
using Application.Settings.UpdateRegistration;
using Application.Shopping;
using Application.Suggestions.Dismiss;
using Application.Suggestions.GetAll;
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
            .AddScoped<IQueryHandler<GetAssistanceSettingsQuery,
                Contracts.Settings.GetAssistance.Response>, GetAssistanceSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UpdateAssistanceSettingsCommand,
                Contracts.Settings.UpdateAssistance.Response>,
                UpdateAssistanceSettingsCommandHandler>()
            .AddScoped<IQueryHandler<GetAssistanceUsageQuery,
                Contracts.Settings.GetAssistanceUsage.Response>, GetAssistanceUsageQueryHandler>()
            .AddScoped<IQueryHandler<GetAssistanceModelsQuery,
                Contracts.Settings.GetAssistanceModels.Response>, GetAssistanceModelsQueryHandler>()

            // The assistant
            .AddScoped<AssistantRun>()
            .AddScoped<RecipeImageWriter>()
            .AddScoped<ICommandHandler<DrawRecipeImageCommand, DrawingProgress>,
                DrawRecipeImageCommandHandler>()
            .AddScoped<ICommandHandler<ComposeRecipeDraftCommand, DraftProgress>,
                ComposeRecipeDraftCommandHandler>()

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
            .AddScoped<IQueryHandler<GetUnitsQuery, Contracts.Recipes.GetUnits.Response>,
                GetUnitsQueryHandler>()
            .AddScoped<IQueryHandler<GetIngredientsQuery, Contracts.Recipes.GetIngredients.Response>,
                GetIngredientsQueryHandler>()
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
            .AddScoped<IQueryHandler<GetRecipeImageQuery, ImageDelivery>, GetRecipeImageQueryHandler>()
            .AddScoped<IQueryHandler<GetShareQuery, Contracts.Recipes.Share.Response>,
                GetShareQueryHandler>()
            .AddScoped<ICommandHandler<CreateShareCommand, Contracts.Recipes.Share.Response>,
                CreateShareCommandHandler>()
            .AddScoped<ICommandHandler<RevokeShareCommand>, RevokeShareCommandHandler>()
            .AddScoped<IQueryHandler<GetSharedRecipeQuery, Contracts.Recipes.GetShared.Response>,
                GetSharedRecipeQueryHandler>()
            .AddScoped<IQueryHandler<GetSharedImageQuery, ImageDelivery>, GetSharedImageQueryHandler>()
            .AddScoped<ICommandHandler<SetCookPhotoCommand, Contracts.Recipes.GetCookLog.Response>,
                SetCookPhotoCommandHandler>()
            .AddScoped<ICommandHandler<RemoveCookPhotoCommand, Contracts.Recipes.GetCookLog.Response>,
                RemoveCookPhotoCommandHandler>()
            .AddScoped<IQueryHandler<GetCookPhotoQuery, ImageDelivery>, GetCookPhotoQueryHandler>()
            .AddScoped<IQueryHandler<ExportArchiveQuery, ArchiveWritten>, ExportArchiveQueryHandler>()
            .AddScoped<ICommandHandler<RestoreArchiveCommand, ArchiveRestored>,
                RestoreArchiveCommandHandler>()
            .AddScoped<IQueryHandler<ImportRecipeQuery, Contracts.Recipes.Import.Response>,
                ImportRecipeQueryHandler>()

            // Connected libraries
            .AddScoped<ICommandHandler<ConnectSourceCommand, Contracts.Recipes.Sources.SourceSummary>,
                ConnectSourceCommandHandler>()
            .AddScoped<IQueryHandler<GetSourcesQuery, Contracts.Recipes.Sources.SourcesResponse>,
                GetSourcesQueryHandler>()
            .AddScoped<ICommandHandler<DisconnectSourceCommand>, DisconnectSourceCommandHandler>()
            .AddScoped<IQueryHandler<BrowseSourceQuery,
                Contracts.Recipes.Sources.SourceRecipesResponse>, BrowseSourceQueryHandler>()
            .AddScoped<ICommandHandler<ImportFromSourceCommand,
                Contracts.Recipes.Sources.ImportStartedResponse>, ImportFromSourceCommandHandler>()
            .AddScoped<IQueryHandler<WatchImportQuery, ImportProgress>, WatchImportQueryHandler>()
            // One recipe's worth of work, resolved once per recipe: an import
            // runs them in parallel and a unit of work is a connection.
            .AddScoped<RecipeImporter>()
            // Singletons: an import outlives the request that asked for it, and
            // the runs a watcher reconnects to are the ones this process holds.
            .AddSingleton<ImportRuns>()
            .AddSingleton<SourceImportRunner>()
            .AddScoped<IQueryHandler<GetMealPlanQuery, Contracts.Planning.MealPlanResponse>,
                GetMealPlanQueryHandler>()
            .AddScoped<ICommandHandler<PlanMealCommand, Contracts.Planning.MealPlanResponse>,
                PlanMealCommandHandler>()
            .AddScoped<ICommandHandler<UnplanMealCommand, Contracts.Planning.MealPlanResponse>,
                UnplanMealCommandHandler>()
            .AddScoped<ICommandHandler<MoveMealCommand, Contracts.Planning.MealPlanResponse>,
                MoveMealCommandHandler>()
            .AddScoped<IQueryHandler<GetTagsQuery, Contracts.Recipes.GetTags.Response>,
                GetTagsQueryHandler>()
            .AddScoped<IQueryHandler<GetCookbooksQuery, Contracts.Cookbooks.CookbooksResponse>,
                GetCookbooksQueryHandler>()
            .AddScoped<IQueryHandler<GetCookbookQuery, Contracts.Cookbooks.CookbookDetail>,
                GetCookbookQueryHandler>()
            .AddScoped<ICommandHandler<CreateCookbookCommand, Contracts.Cookbooks.CookbookDetail>,
                CreateCookbookCommandHandler>()
            .AddScoped<ICommandHandler<UpdateCookbookCommand, Contracts.Cookbooks.CookbookDetail>,
                UpdateCookbookCommandHandler>()
            .AddScoped<ICommandHandler<DeleteCookbookCommand>, DeleteCookbookCommandHandler>()
            .AddScoped<ICommandHandler<AddRecipeToCookbookCommand>,
                AddRecipeToCookbookCommandHandler>()
            .AddScoped<ICommandHandler<RemoveRecipeFromCookbookCommand>,
                RemoveRecipeFromCookbookCommandHandler>()
            .AddScoped<IQueryHandler<GetRecipeCookbooksQuery, Contracts.Cookbooks.RecipeCookbooksResponse>,
                GetRecipeCookbooksQueryHandler>()

            // Suggestions
            .AddScoped<IQueryHandler<GetSuggestionsQuery, Contracts.Suggestions.GetAll.Response>,
                GetSuggestionsQueryHandler>()
            .AddScoped<ICommandHandler<DismissSuggestionCommand>, DismissSuggestionCommandHandler>()

            // Saved searches
            .AddScoped<IQueryHandler<GetSavedSearchesQuery, Contracts.Searches.SavedSearchesResponse>,
                GetSavedSearchesQueryHandler>()
            .AddScoped<ICommandHandler<CreateSavedSearchCommand, Contracts.Searches.SavedSearchDetail>,
                CreateSavedSearchCommandHandler>()
            .AddScoped<ICommandHandler<UpdateSavedSearchCommand, Contracts.Searches.SavedSearchDetail>,
                UpdateSavedSearchCommandHandler>()
            .AddScoped<ICommandHandler<DeleteSavedSearchCommand>, DeleteSavedSearchCommandHandler>();
    }
}
