using Application.Abstractions.Messaging;
using Application.AgentAuthorizations;
using Application.AgentAuthorizations.Decide;
using Application.AgentAuthorizations.GetById;
using Application.AgentAuthorizations.Redeem;
using Application.AgentAuthorizations.Register;
using Application.AgentAuthorizations.Start;
using Application.Archive;
using Application.EmailConfirmations.Create;
using Application.Mail;
using Application.PasswordResetLinks.Create;
using Application.Settings.GetMail;
using Application.Settings.SendMailTest;
using Application.Settings.UpdateMail;
using Application.SignInLinks.Create;
using Application.SignInLinks.Redeem;
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
using Application.Households.GetHeirs;
using Application.Households.GetInvitationByCode;
using Application.Households.GetInvitations;
using Application.Households.GetMembers;
using Application.Households.GetTrash;
using Application.Households.RedeemInvitation;
using Application.Households.RemoveHeir;
using Application.Households.RemoveMember;
using Application.Households.Rename;
using Application.Households.Restore;
using Application.Households.RevokeInvitation;
using Application.Households.SetInheritance;
using Application.Ingredients;
using Application.LogRecords.Create;
using Application.Nutrition;
using Application.PasswordResets.Create;
using Application.Planning;
using Application.Products;
using Application.Recipes;
using Application.Recipes.Copy;
using Application.Recipes.Create;
using Application.Recipes.CreateShare;
using Application.Recipes.Delete;
using Application.Recipes.Drafts;
using Application.Recipes.DrawImage;
using Application.Recipes.GetAll;
using Application.Recipes.GetById;
using Application.Recipes.GetCompletions;
using Application.Recipes.GetCookLog;
using Application.Recipes.GetImage;
using Application.Recipes.GetIngredients;
using Application.Recipes.GetNotes;
using Application.Recipes.GetNutrition;
using Application.Recipes.GetRelated;
using Application.Recipes.GetShare;
using Application.Recipes.GetShared;
using Application.Recipes.GetSharedImage;
using Application.Recipes.GetSharedNutrition;
using Application.Recipes.GetTags;
using Application.Recipes.GetTagSuggestions;
using Application.Recipes.GetUnits;
using Application.Recipes.Import;
using Application.Recipes.RecordCooked;
using Application.Recipes.RemoveImage;
using Application.Recipes.Restore;
using Application.Recipes.RevokeShare;
using Application.Recipes.SaveNotes;
using Application.Recipes.SetImage;
using Application.Recipes.Sources;
using Application.Recipes.UndoCooked;
using Application.Recipes.Update;
using Application.RecoveryCodes.Issue;
using Application.Registration.GetPolicy;
using Application.Searches;
using Application.Sessions;
using Application.Sessions.GetAll;
using Application.Sessions.Revoke;
using Application.Sessions.SignIn;
using Application.Settings;
using Application.Settings.GetAssistance;
using Application.Settings.GetAssistanceModels;
using Application.Settings.GetAssistanceUsage;
using Application.Settings.GetDatabase;
using Application.Settings.GetRegistration;
using Application.Settings.GetServer;
using Application.Settings.UpdateAssistance;
using Application.Settings.UpdateDatabase;
using Application.Settings.UpdateRegistration;
using Application.Settings.UpdateServer;
using Application.Setup.Get;
using Application.Shopping;
using Application.Suggestions.Dismiss;
using Application.Suggestions.GetAll;
using Application.Suggestions.RecordImpressions;
using Application.Trash.Purge;
using Application.Users;
using Application.Users.ChangePassword;
using Application.Users.CreateRecoveryCodes;
using Application.Users.GetCurrent;
using Application.Users.GetPreferences;
using Application.Users.GetRecoveryCodes;
using Application.Users.Register;
using Application.Users.UpdateCurrent;
using Application.Users.UpdatePreferences;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>Registers every command and query handler, explicitly, one line each.</summary>
/// <remarks>
/// No scanning, so a handler never registered fails the build (an architecture test checks);
/// scoped, as they hold a unit of work.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Adds the use cases.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Grouped by domain, in folder order; the order itself does not matter.
        return services
            // Users
            .AddScoped<Application.Recipes.Intake.IntakeProcessor>()
            .AddScoped<Application.Recipes.Intake.DraftToRecipe>()
            .AddScoped<Application.Recipes.Intake.IntakeSubmission>()
            .AddScoped<ICommandHandler<Application.Recipes.Intake.StartRecipeIntakeCommand, Contracts.Recipes.Intake.IntakeJob>,
                Application.Recipes.Intake.StartRecipeIntakeCommandHandler>()
            .AddScoped<ICommandHandler<Application.Recipes.Intake.RetryRecipeIntakeCommand, Contracts.Recipes.Intake.IntakeJob>,
                Application.Recipes.Intake.RetryRecipeIntakeCommandHandler>()
            .AddScoped<ICommandHandler<Application.Recipes.Intake.StartRecipeDraftCommand, Contracts.Recipes.Intake.IntakeJob>,
                Application.Recipes.Intake.StartRecipeDraftCommandHandler>()
            .AddScoped<Application.Recipes.Intake.IntakeWatch>()
            // A singleton: a stream listens for changes made by the worker's own scope.
            .AddSingleton<Application.Recipes.Intake.IntakeChanges>()
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
            // The food diary: one person's, never a household's.
            .AddScoped<Application.Users.DiaryLookup>()
            .AddScoped<IQueryHandler<Application.Users.GetDiarySettings.GetDiarySettingsQuery, Contracts.Users.GetDiarySettings.Response>,
                Application.Users.GetDiarySettings.GetDiarySettingsQueryHandler>()
            .AddScoped<ICommandHandler<Application.Users.UpdateDiarySettings.UpdateDiarySettingsCommand, Contracts.Users.UpdateDiarySettings.Response>,
                Application.Users.UpdateDiarySettings.UpdateDiarySettingsCommandHandler>()
            .AddScoped<IQueryHandler<Application.Users.GetDiary.GetDiaryQuery, Contracts.Users.GetDiary.Response>,
                Application.Users.GetDiary.GetDiaryQueryHandler>()
            .AddScoped<ICommandHandler<Application.Users.AddDiaryEntry.AddDiaryEntryCommand, Contracts.Users.AddDiaryEntry.Response>,
                Application.Users.AddDiaryEntry.AddDiaryEntryCommandHandler>()
            .AddScoped<ICommandHandler<Application.Users.UpdateDiaryEntry.UpdateDiaryEntryCommand, Contracts.Users.UpdateDiaryEntry.Response>,
                Application.Users.UpdateDiaryEntry.UpdateDiaryEntryCommandHandler>()
            .AddScoped<ICommandHandler<Application.Users.RemoveDiaryEntry.RemoveDiaryEntryCommand>,
                Application.Users.RemoveDiaryEntry.RemoveDiaryEntryCommandHandler>()
            .AddScoped<ICommandHandler<Application.Users.EraseDiary.EraseDiaryCommand>,
                Application.Users.EraseDiary.EraseDiaryCommandHandler>()
            .AddScoped<ICommandHandler<ChangePasswordCommand>, ChangePasswordCommandHandler>()
            .AddScoped<ICommandHandler<CreateRecoveryCodesCommand, Contracts.Users.CreateRecoveryCodes.Response>,
                CreateRecoveryCodesCommandHandler>()
            .AddScoped<IQueryHandler<GetRecoveryCodesQuery, Contracts.Users.GetRecoveryCodes.Response>,
                GetRecoveryCodesQueryHandler>()
            .AddScoped<PasswordConfirmation>()

            // Account recovery
            .AddScoped<ICommandHandler<IssueRecoveryCodeCommand, Contracts.RecoveryCodes.Issue.Response>,
                IssueRecoveryCodeCommandHandler>()
            .AddScoped<ICommandHandler<ResetPasswordCommand>, ResetPasswordCommandHandler>()

            // Mail
            .AddScoped<AccountMail>()
            // A singleton: what it remembers is shared by every request.
            .AddSingleton<MailThrottle>()
            .AddScoped<ICommandHandler<RequestPasswordResetLinkCommand>, RequestPasswordResetLinkCommandHandler>()
            .AddScoped<ICommandHandler<RequestSignInLinkCommand>, RequestSignInLinkCommandHandler>()
            .AddScoped<ICommandHandler<RedeemSignInLinkCommand, SignInOutcome>, RedeemSignInLinkCommandHandler>()
            .AddScoped<ICommandHandler<ConfirmEmailCommand, SignInOutcome>, ConfirmEmailCommandHandler>()
            .AddScoped<LinkSignIn>()
            .AddScoped<NewDeviceNotice>()
            .AddScoped<IQueryHandler<GetMailSettingsQuery, Contracts.Settings.GetMail.Response>,
                GetMailSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UpdateMailSettingsCommand, Contracts.Settings.GetMail.Response>,
                UpdateMailSettingsCommandHandler>()
            .AddScoped<ICommandHandler<SendMailTestCommand>, SendMailTestCommandHandler>()

            // Sessions
            .AddScoped<ICommandHandler<SignInCommand, SignInOutcome>, SignInCommandHandler>()
            .AddScoped<ICommandHandler<RevokeSessionCommand>, RevokeSessionCommandHandler>()
            .AddScoped<IQueryHandler<GetSessionsQuery, Contracts.Sessions.GetAll.Response>,
                GetSessionsQueryHandler>()
            .AddScoped<SessionStarter>()

            // Connecting AI agents through OAuth
            .AddScoped<ClientRegistrations>()
            .AddScoped<ICommandHandler<RegisterAgentClientCommand, RegisteredAgentClient>, RegisterAgentClientCommandHandler>()
            .AddScoped<ICommandHandler<StartAgentAuthorizationCommand, AuthorizationStep>,
                StartAgentAuthorizationCommandHandler>()
            .AddScoped<IQueryHandler<GetAgentAuthorizationByIdQuery, Contracts.AgentAuthorizations.GetById.Response>,
                GetAgentAuthorizationByIdQueryHandler>()
            .AddScoped<ICommandHandler<DecideAgentAuthorizationCommand, Contracts.AgentAuthorizations.Decide.Response>,
                DecideAgentAuthorizationCommandHandler>()
            .AddScoped<ICommandHandler<RedeemAuthorizationCodeCommand, AgentAccessToken>,
                RedeemAuthorizationCodeCommandHandler>()

            // Log records
            .AddScoped<ICommandHandler<CreateLogRecordsCommand>, CreateLogRecordsCommandHandler>()

            // Households
            .AddScoped<IQueryHandler<GetHouseholdsQuery, Contracts.Households.GetAll.Response>,
                GetHouseholdsQueryHandler>()
            .AddScoped<ICommandHandler<CreateHouseholdCommand, Contracts.Households.Create.Response>,
                CreateHouseholdCommandHandler>()
            .AddScoped<IQueryHandler<GetHouseholdQuery, Contracts.Households.GetById.Response>,
                GetHouseholdQueryHandler>()
            .AddScoped<ICommandHandler<RenameHouseholdCommand, Contracts.Households.Rename.Response>,
                RenameHouseholdCommandHandler>()
            .AddScoped<ICommandHandler<SetInheritanceCommand, Contracts.Households.SetInheritance.Response>,
                SetInheritanceCommandHandler>()
            .AddScoped<IQueryHandler<GetHeirsQuery, Contracts.Households.GetHeirs.Response>,
                GetHeirsQueryHandler>()
            .AddScoped<ICommandHandler<RemoveHeirCommand>, RemoveHeirCommandHandler>()
            .AddScoped<ICommandHandler<DeleteHouseholdCommand>, DeleteHouseholdCommandHandler>()
            .AddScoped<ICommandHandler<RestoreHouseholdCommand>, RestoreHouseholdCommandHandler>()
            .AddScoped<IQueryHandler<GetTrashQuery, Contracts.Households.GetTrash.Response>,
                GetTrashQueryHandler>()
            .AddScoped<ICommandHandler<PurgeTrashCommand, int>, PurgeTrashCommandHandler>()
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
            .AddScoped<IQueryHandler<GetInvitationByCodeQuery,
                Contracts.Households.GetInvitationByCode.Response>, GetInvitationByCodeQueryHandler>()

            // Server settings, beyond what setting up needs
            .AddSetupHandlers()
            .AddScoped<IQueryHandler<GetServerSettingsQuery, Contracts.Settings.GetServer.Response>,
                GetServerSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UpdateServerSettingsCommand, ServerChange>,
                UpdateServerSettingsCommandHandler>()

            // Instance settings
            .AddScoped<IQueryHandler<GetRegistrationSettingsQuery,
                Contracts.Settings.GetRegistration.Response>, GetRegistrationSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UndoCookedCommand>, UndoCookedCommandHandler>()
            .AddScoped<ICommandHandler<SetFoodCorrectionCommand>, SetFoodCorrectionCommandHandler>()
            .AddScoped<ICommandHandler<RemoveFoodCorrectionCommand>, RemoveFoodCorrectionCommandHandler>()
            .AddScoped<ICommandHandler<SetUnitWeightCommand>, SetUnitWeightCommandHandler>()
            .AddScoped<ICommandHandler<RemoveUnitWeightCommand>, RemoveUnitWeightCommandHandler>()
            .AddScoped<IQueryHandler<GetNutritionSettingsQuery, Contracts.Nutrition.NutritionSettings>, GetNutritionSettingsQueryHandler>()
            .AddScoped<ICommandHandler<SetNutritionSettingsCommand, Contracts.Nutrition.NutritionSettings>, SetNutritionSettingsCommandHandler>()
            .AddScoped<IQueryHandler<GetIngredientFactsQuery, Contracts.Nutrition.IngredientFactsResponse>, GetIngredientFactsQueryHandler>()
            .AddScoped<IQueryHandler<GetHouseholdFoodsQuery, Contracts.Nutrition.HouseholdFoodsResponse>, GetHouseholdFoodsQueryHandler>()
            .AddScoped<ICommandHandler<CreateHouseholdFoodCommand, Contracts.Nutrition.HouseholdFoodDetail>, CreateHouseholdFoodCommandHandler>()
            .AddScoped<ICommandHandler<DeleteHouseholdFoodCommand>, DeleteHouseholdFoodCommandHandler>()
            .AddScoped<IQueryHandler<GetFoodsQuery, Contracts.Nutrition.FoodsResponse>, GetFoodsQueryHandler>()
            .AddScoped<IQueryHandler<GetProductQuery, Contracts.Products.ProductDetail>, GetProductQueryHandler>()
            .AddScoped<IQueryHandler<ListProductsQuery, Contracts.Products.ProductsResponse>, ListProductsQueryHandler>()
            .AddScoped<ICommandHandler<SetProductCommand, Contracts.Products.ProductDetail>, SetProductCommandHandler>()
            .AddScoped<ICommandHandler<RemoveProductCommand>, RemoveProductCommandHandler>()
            .AddScoped<IQueryHandler<GetShoppingListQuery,
                Contracts.Shopping.Response>, GetShoppingListQueryHandler>()
            .AddScoped<ICommandHandler<AddShoppingItemCommand,
                Contracts.Shopping.Response>, AddShoppingItemCommandHandler>()
            .AddScoped<ICommandHandler<AddRecipeToListCommand,
                Contracts.Shopping.Response>, AddRecipeToListCommandHandler>()
            .AddScoped<ICommandHandler<AddPlannedMealsToListCommand,
                Contracts.Shopping.Response>, AddPlannedMealsToListCommandHandler>()
            .AddScoped<ICommandHandler<WithdrawPlannedMealCommand,
                Contracts.Shopping.Response>, WithdrawPlannedMealCommandHandler>()
            .AddScoped<ICommandHandler<WithdrawRecipeCommand,
                Contracts.Shopping.Response>, WithdrawRecipeCommandHandler>()
            .AddScoped<ICommandHandler<RefreshPlannedMealOnListCommand,
                Contracts.Shopping.Response>, RefreshPlannedMealOnListCommandHandler>()
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
            .AddScoped<ImageRelease>()
            .AddScoped<RecipeImageWriter>()
            .AddScoped<ICommandHandler<DrawRecipeImageCommand, DrawingProgress>,
                DrawRecipeImageCommandHandler>()
            .AddScoped<ICommandHandler<ComposeRecipeDraftCommand, DraftProgress>,
                ComposeRecipeDraftCommandHandler>()

            // Recipes
            .AddScoped<ICommandHandler<CreateRecipeCommand, Contracts.Recipes.RecipeDetail>,
                CreateRecipeCommandHandler>()
            .AddScoped<ICommandHandler<CopyRecipeCommand, Contracts.Recipes.RecipeDetail>,
                CopyRecipeCommandHandler>()
            .AddScoped<IQueryHandler<GetRecipesQuery, Contracts.Recipes.GetAll.Response>,
                GetRecipesQueryHandler>()
            .AddScoped<IQueryHandler<GetRecipeQuery, Contracts.Recipes.RecipeDetail>,
                GetRecipeQueryHandler>()
            .AddScoped<IQueryHandler<GetNutritionQuery, RecipeNutrition>, GetNutritionQueryHandler>()
            .AddScoped<ICommandHandler<UpdateRecipeCommand, Contracts.Recipes.RecipeDetail>,
                UpdateRecipeCommandHandler>()
            .AddScoped<ICommandHandler<DeleteRecipeCommand>, DeleteRecipeCommandHandler>()
            .AddScoped<ICommandHandler<RestoreRecipeCommand>, RestoreRecipeCommandHandler>()
            .AddScoped<IQueryHandler<GetUnitsQuery, Contracts.Recipes.GetUnits.Response>,
                GetUnitsQueryHandler>()
            .AddScoped<IQueryHandler<GetIngredientsQuery, Contracts.Recipes.GetIngredients.Response>,
                GetIngredientsQueryHandler>()
            .AddScoped<IQueryHandler<GetCompletionsQuery, Contracts.Recipes.GetCompletions.Response>,
                GetCompletionsQueryHandler>()
            .AddScoped<IQueryHandler<GetRelatedRecipesQuery, Contracts.Recipes.GetRelated.Response>,
                GetRelatedRecipesQueryHandler>()
            .AddScoped<IQueryHandler<GetTagSuggestionsQuery, Contracts.Recipes.GetTagSuggestions.Response>,
                GetTagSuggestionsQueryHandler>()
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
            .AddScoped<IQueryHandler<GetSharedNutritionQuery, SharedNutrition>, GetSharedNutritionQueryHandler>()
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
            // One recipe's worth of work, resolved once per recipe: imports run them in parallel.
            .AddScoped<RecipeImporter>()
            // Singletons: an import outlives its request, and a watcher reconnects to runs this
            // process holds.
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
            .AddScoped<IQueryHandler<GetCookbookRecipesQuery, Contracts.Cookbooks.CookbookRecipesResponse>,
                GetCookbookRecipesQueryHandler>()
            .AddScoped<ICommandHandler<CreateCookbookCommand, Contracts.Cookbooks.CookbookDetail>,
                CreateCookbookCommandHandler>()
            .AddScoped<ICommandHandler<UpdateCookbookCommand, Contracts.Cookbooks.CookbookDetail>,
                UpdateCookbookCommandHandler>()
            .AddScoped<ICommandHandler<DeleteCookbookCommand>, DeleteCookbookCommandHandler>()
            .AddScoped<ICommandHandler<RestoreCookbookCommand>, RestoreCookbookCommandHandler>()
            .AddScoped<ICommandHandler<AddRecipeToCookbookCommand>,
                AddRecipeToCookbookCommandHandler>()
            .AddScoped<ICommandHandler<RemoveRecipeFromCookbookCommand>,
                RemoveRecipeFromCookbookCommandHandler>()
            .AddScoped<ICommandHandler<ReorderCookbookRecipesCommand, Contracts.Cookbooks.CookbookDetail>,
                ReorderCookbookRecipesCommandHandler>()
            .AddScoped<IQueryHandler<GetRecipeCookbooksQuery, Contracts.Cookbooks.RecipeCookbooksResponse>,
                GetRecipeCookbooksQueryHandler>()

            // Suggestions
            .AddScoped<IQueryHandler<GetSuggestionsQuery, Contracts.Suggestions.GetAll.Response>,
                GetSuggestionsQueryHandler>()
            .AddScoped<ICommandHandler<DismissSuggestionCommand>, DismissSuggestionCommandHandler>()
            .AddScoped<ICommandHandler<RecordImpressionsCommand>, RecordImpressionsCommandHandler>()

            // Saved searches
            .AddScoped<IQueryHandler<GetSavedSearchesQuery, Contracts.Searches.SavedSearchesResponse>,
                GetSavedSearchesQueryHandler>()
            .AddScoped<ICommandHandler<CreateSavedSearchCommand, Contracts.Searches.SavedSearchDetail>,
                CreateSavedSearchCommandHandler>()
            .AddScoped<ICommandHandler<UpdateSavedSearchCommand, Contracts.Searches.SavedSearchDetail>,
                UpdateSavedSearchCommandHandler>()
            .AddScoped<ICommandHandler<DeleteSavedSearchCommand>, DeleteSavedSearchCommandHandler>()

            // Ingredient names
            .AddScoped<IQueryHandler<GetIngredientNamesQuery, Contracts.Ingredients.IngredientNamesResponse>,
                GetIngredientNamesQueryHandler>()
            .AddScoped<IQueryHandler<GetIngredientMergesQuery, Contracts.Ingredients.IngredientMergesResponse>,
                GetIngredientMergesQueryHandler>()
            .AddScoped<ICommandHandler<CreateIngredientMergeCommand, Contracts.Ingredients.CreatedIngredientMerge>,
                CreateIngredientMergeCommandHandler>()
            .AddScoped<ICommandHandler<DeleteIngredientMergeCommand>, DeleteIngredientMergeCommandHandler>()
            .AddScoped<IQueryHandler<GetMergeSuggestionsQuery, Contracts.Ingredients.MergeSuggestionsResponse>,
                GetMergeSuggestionsQueryHandler>()
            .AddScoped<ICommandHandler<DeleteMergeSuggestionCommand>, DeleteMergeSuggestionCommandHandler>()
            .AddScoped<IQueryHandler<GetIngredientNounsQuery, Contracts.Ingredients.IngredientNounsResponse>,
                GetIngredientNounsQueryHandler>()
            .AddScoped<ICommandHandler<SetIngredientNounCommand>, SetIngredientNounCommandHandler>()
            .AddScoped<ICommandHandler<DeleteIngredientNounCommand>, DeleteIngredientNounCommandHandler>();
    }

    /// <summary>
    /// The use cases the pre-database host can serve: setup progress and database settings.
    /// </summary>
    /// <remarks>
    /// Also part of <see cref="AddApplication"/>: the same routes exist once there is a database.
    /// </remarks>
    public static IServiceCollection AddSetupHandlers(this IServiceCollection services) =>
        services
            .AddScoped<IQueryHandler<GetSetupQuery, Contracts.Setup.Get.Response>, GetSetupQueryHandler>()
            .AddScoped<IQueryHandler<GetDatabaseSettingsQuery, Contracts.Settings.GetDatabase.Response>,
                GetDatabaseSettingsQueryHandler>()
            .AddScoped<ICommandHandler<UpdateDatabaseSettingsCommand, ServerChange>,
                UpdateDatabaseSettingsCommandHandler>();
}
