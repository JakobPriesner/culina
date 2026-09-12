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

    internal const string Settings = "Settings";

    internal const string Registration = "Registration";

    internal const string CookSessions = "CookSessions";

    internal const string Shopping = "Shopping";
}
