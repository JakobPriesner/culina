namespace Api.Agents;

/// <summary>The API operations an AI agent may call, each offered as one MCP tool of the same name.</summary>
/// <remarks>
/// An allow-list, so a new endpoint is withheld until someone decides otherwise: AgentOperationsTests fails until
/// every endpoint is either here or in its withheld list with a reason. An agent's session authenticates only these
/// and the MCP endpoint itself (see <see cref="AgentAccess"/>), so the list is the whole of what a leaked agent token
/// can do.
///
/// Withheld on purpose: the account and its credentials (sign-in, sessions, password, recovery codes, preferences),
/// the instance's settings, push devices, household membership and invitations (which would let an agent hand the
/// kitchen to someone else), creating, renaming, deleting or restoring a household and what it inherits, share links
/// (which publish a recipe), connected libraries (which hold another app's credentials), whole-household archives,
/// the assistant (it costs money, and the agent is a model already), and binary, multipart or streaming endpoints
/// a tool cannot carry.
/// </remarks>
internal static class AgentOperations
{
    internal static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // Who is asking, and which kitchens they belong to: most tools need a householdId.
        "getCurrentUserV1",
        "getUserSettingsV1",
        "getHouseholdsV1",
        "getHouseholdByIdV1",
        "getTrashV1",

        // Recipes.
        "getRecipesV1",
        "getRecipeByIdV1",
        "createRecipeV1",
        "updateRecipeV1",
        "deleteRecipeV1",
        "restoreRecipeV1",
        "copyRecipeV1",
        "importRecipeV1",
        "removeRecipeImageV1",
        "getRecipeNutritionV1",
        "getRelatedRecipesV1",
        "getRecipeTagSuggestionsV1",
        "getRecipeCookbooksV1",
        "getTagsV1",
        "getHouseholdUnitsV1",
        "getHouseholdIngredientsV1",
        "getHouseholdCompletionsV1",

        // A cook's own notes and history.
        "getRecipeNotesV1",
        "saveRecipeNotesV1",
        "getRecipeCookLogV1",
        "recordRecipeCookedV1",
        "undoRecipeCookedV1",
        "removeCookPhotoV1",

        // Cooking along.
        "startCookSessionV1",
        "getCurrentCookSessionV1",
        "updateCookSessionV1",
        "endCookSessionV1",

        // Cookbooks.
        "getCookbooksV1",
        "getCookbookByIdV1",
        "createCookbookV1",
        "updateCookbookV1",
        "deleteCookbookV1",
        "restoreCookbookV1",
        "getCookbookRecipesV1",
        "reorderCookbookRecipesV1",
        "addRecipeToCookbookV1",
        "removeRecipeFromCookbookV1",

        // Saved searches and suggestions.
        "getSavedSearchesV1",
        "createSavedSearchV1",
        "updateSavedSearchV1",
        "deleteSavedSearchV1",
        "getSuggestionsV1",
        "dismissSuggestionV1",
        "restoreSuggestionV1",

        // The meal plan.
        "getMealPlanV1",
        "planMealV1",
        "moveMealV1",
        "unplanMealV1",

        // The shopping list.
        "getShoppingListV1",
        "addShoppingItemV1",
        "updateShoppingItemV1",
        "removeShoppingItemsV1",
        "addRecipeToShoppingListV1",
        "withdrawRecipeFromShoppingListV1",
        "addPlannedMealsToShoppingListV1",
        "refreshPlannedMealOnShoppingListV1",
        "withdrawPlannedMealFromShoppingListV1",

        // Ingredients and nutrition.
        "getFoodsV1",
        "getHouseholdNutritionV1",
        "setHouseholdNutritionV1",
        "getHouseholdNutritionIngredientsV1",
        "setHouseholdIngredientFoodV1",
        "removeHouseholdIngredientFoodV1",
        "setHouseholdIngredientUnitWeightV1",
        "removeHouseholdIngredientUnitWeightV1",
        "getHouseholdFoodsV1",
        "createHouseholdFoodV1",
        "deleteHouseholdFoodV1",
        "getIngredientNamesV1",
        "getIngredientMergesV1",
        "createIngredientMergeV1",
        "deleteIngredientMergeV1",
        "getIngredientMergeSuggestionsV1",
        "deleteIngredientMergeSuggestionV1",
        "getIngredientNounsV1",
        "setIngredientNounV1",
        "deleteIngredientNounV1"
    };

    /// <summary>Whether the endpoint is one of the operations an agent may call.</summary>
    internal static bool Includes(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName is { } name
        && Allowed.Contains(name);
}
