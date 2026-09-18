namespace Api.Endpoints;

/// <summary>
/// The OpenAPI tags endpoints are grouped under.
/// </summary>
/// <remarks>
/// One tag per API resource, matching the first path segment, so the generated
/// client groups its methods the same way the routes are organised.
/// </remarks>
internal static class Tags
{
    internal const string Users = "Users";

    internal const string Sessions = "Sessions";

    internal const string Households = "Households";

    internal const string Recipes = "Recipes";

    /// <summary>
    /// What a link hands a stranger, kept apart from <see cref="Recipes"/>.
    /// </summary>
    /// <remarks>
    /// A tag of its own because the distinction is worth seeing in the
    /// generated client: everything under <c>Recipes</c> needs a session, and
    /// these two reads are the only ones that do not.
    /// </remarks>
    internal const string SharedRecipes = "SharedRecipes";

    internal const string Planning = "Planning";

    internal const string Settings = "Settings";

    internal const string Registration = "Registration";

    internal const string CookSessions = "CookSessions";

    internal const string Shopping = "Shopping";

    internal const string Cookbooks = "Cookbooks";

    internal const string RecipeSources = "RecipeSources";

    internal const string Suggestions = "Suggestions";

    internal const string Searches = "Searches";
}
