using Api.Endpoints;
using Api.Endpoints.AgentAuthorizations;
using Api.Endpoints.Cookbooks;
using Api.Endpoints.CookSessions;
using Api.Endpoints.EmailConfirmations;
using Api.Endpoints.Households;
using Api.Endpoints.Ingredients;
using Api.Endpoints.Invitations;
using Api.Endpoints.LogRecords;
using Api.Endpoints.Nutrition;
using Api.Endpoints.PasswordResetLinks;
using Api.Endpoints.PasswordResets;
using Api.Endpoints.Planning;
using Api.Endpoints.Products;
using Api.Endpoints.Push;
using Api.Endpoints.RecipeDrafts;
using Api.Endpoints.Recipes;
using Api.Endpoints.RecipeSources;
using Api.Endpoints.RecoveryCodes;
using Api.Endpoints.Registration;
using Api.Endpoints.Searches;
using Api.Endpoints.Sessions;
using Api.Endpoints.Settings;
using Api.Endpoints.Setup;
using Api.Endpoints.SharedRecipes;
using Api.Endpoints.Shopping;
using Api.Endpoints.SignInLinks;
using Api.Endpoints.Suggestions;
using Api.Endpoints.Users;

namespace Api.Extensions;

/// <summary>Collects and maps every endpoint.</summary>
internal static class EndpointExtensions
{
    /// <summary>
    /// Registers each domain's endpoints, one line per domain; nothing depends on the order.
    /// </summary>
    internal static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddUsersEndpoints()
            .AddPushEndpoints()
            .AddSessionsEndpoints()
            .AddAgentAuthorizationsEndpoints()
            .AddPasswordResetsEndpoints()
            .AddRecoveryCodesEndpoints()
            .AddPasswordResetLinksEndpoints()
            .AddSignInLinksEndpoints()
            .AddEmailConfirmationsEndpoints()
            .AddHouseholdsEndpoints()
            .AddInvitationsEndpoints()
            .AddSettingsEndpoints()
            .AddSetupEndpoints()
            .AddRegistrationEndpoints()
            .AddRecipesEndpoints()
            .AddSharedRecipesEndpoints()
            .AddCookSessionEndpoints()
            .AddShoppingEndpoints()
            .AddNutritionEndpoints()
            .AddProductsEndpoints()
            .AddIngredientsEndpoints()
            .AddPlanningEndpoints()
            .AddCookbooksEndpoints()
            .AddRecipeSourcesEndpoints()
            .AddRecipeDraftsEndpoints()
            .AddSuggestionsEndpoints()
            .AddSearchesEndpoints()
            .AddLogRecordsEndpoints();
    }

    /// <summary>Maps everything registered as an <see cref="IEndpoint"/>.</summary>
    internal static WebApplication MapEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }
}
