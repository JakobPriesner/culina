namespace Api.Endpoints;

/// <summary>
/// The OpenAPI tags endpoints are grouped under, one per resource and matching the first path
/// segment, so the generated client groups methods like the routes.
/// </summary>
internal static class Tags
{
    internal const string Users = "Users";

    internal const string Sessions = "Sessions";

    internal const string PasswordResets = "PasswordResets";

    internal const string RecoveryCodes = "RecoveryCodes";

    internal const string Households = "Households";

    internal const string Recipes = "Recipes";

    /// <summary>
    /// What a link hands a stranger, kept apart from <see cref="Recipes"/>: everything under
    /// Recipes needs a session, and these two reads are the only ones that do not.
    /// </summary>
    internal const string SharedRecipes = "SharedRecipes";

    internal const string Planning = "Planning";

    internal const string Settings = "Settings";

    /// <summary>
    /// How far a fresh instance has got: the one read both hosts serve, the one with a database and
    /// the one waiting for it.
    /// </summary>
    internal const string Setup = "Setup";

    internal const string Registration = "Registration";

    internal const string CookSessions = "CookSessions";

    internal const string Shopping = "Shopping";

    internal const string Nutrition = "Nutrition";

    internal const string Cookbooks = "Cookbooks";

    internal const string RecipeSources = "RecipeSources";

    internal const string Suggestions = "Suggestions";

    internal const string Searches = "Searches";

    internal const string LogRecords = "LogRecords";
}
