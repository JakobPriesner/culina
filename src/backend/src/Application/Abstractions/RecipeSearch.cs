namespace Application.Abstractions;

/// <summary>What to look for.</summary>
/// <param name="HouseholdId">Whose recipes.</param>
/// <param name="UserId">Who is asking, for their own cook counts.</param>
/// <param name="Query">Free text over title, description and ingredient names.</param>
/// <param name="Tags">Tag slugs, all of which must be present.</param>
/// <param name="Ingredients">Ingredients the caller has, for ranking.</param>
/// <param name="MaxMinutes">A ceiling on total time.</param>
/// <param name="CookbookId">
/// Only what is on this shelf by hand, or null. A smart shelf does not set
/// this — it sets <paramref name="Rules"/> instead, because what is on it was
/// never written down anywhere.
/// </param>
/// <param name="Rules">What a smart shelf asks for, or null.</param>
/// <param name="Sort">How to order the results.</param>
/// <param name="Cursor">Where the previous page ended.</param>
/// <param name="Limit">How many to return.</param>
public sealed record RecipeSearch(
    Guid HouseholdId,
    Guid UserId,
    string? Query,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Ingredients,
    int? MaxMinutes,
    Guid? CookbookId,
    RecipeRules? Rules,
    RecipeSort Sort,
    string? Cursor,
    int Limit);

/// <summary>The orders a recipe list can be returned in.</summary>
public enum RecipeSort
{
    /// <summary>Most recently changed first. The default.</summary>
    RecentFirst = 0,

    /// <summary>Alphabetical.</summary>
    Title = 1,

    /// <summary>Quickest first. "I have 25 minutes" is the real constraint.</summary>
    ShortestFirst = 2,

    /// <summary>What the caller cooks most.</summary>
    MostCooked = 3,

    /// <summary>Best fit for the query and the named ingredients.</summary>
    Relevance = 4,

    /// <summary>
    /// The order a cookbook was built in. Only legal with a cookbook, and the
    /// default when there is one.
    /// </summary>
    CookbookOrder = 5,

    /// <summary>
    /// What this person would most likely want to cook, now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sort and not a second collection, for the same reason a cookbook is a
    /// view of the library rather than one of its own: every filter above
    /// composes with it for free, so "what should I cook?" and "I have
    /// twenty-five minutes and some chicken" are one feature rather than two
    /// that can disagree.
    /// </para>
    /// <para>
    /// Scored as of the current day rather than the current instant, so the
    /// order is stable for as long as somebody is looking at it and a cursor
    /// still means something on the second page.
    /// </para>
    /// </remarks>
    Suggested = 6
}

/// <summary>
/// What a smart cookbook asks for, as the searcher needs it.
/// </summary>
/// <remarks>
/// A copy of the domain's rules rather than the domain type itself, because
/// this is a port: <c>Application.Abstractions</c> describes what the database
/// is asked, and a search that took a <c>Cookbook</c> would make every caller
/// of the recipe list know what a cookbook is.
/// </remarks>
/// <param name="Tags">Tag slugs a recipe must all carry.</param>
/// <param name="Ingredients">Ingredient names a recipe must all use.</param>
/// <param name="MaxMinutes">The longest a recipe may take, or null.</param>
public sealed record RecipeRules(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Ingredients,
    int? MaxMinutes);

/// <summary>One page of matching recipes.</summary>
/// <param name="Items">The rows, already ordered.</param>
/// <param name="NextCursor">Where this page ended, or null at the end.</param>
/// <param name="Total">How many match across all pages.</param>
public sealed record RecipePage(
    IReadOnlyList<RecipeSearchRow> Items,
    string? NextCursor,
    int Total);

/// <summary>One matching recipe, with everything a card needs.</summary>
/// <param name="RecipeId">Its id.</param>
/// <param name="Title">What it is called.</param>
/// <param name="ImageId">Its hero image.</param>
/// <param name="TotalMinutes">Prep plus cook, or null.</param>
/// <param name="YieldAmount">How many it makes.</param>
/// <param name="YieldKind">Of what.</param>
/// <param name="Tags">Its tag slugs.</param>
/// <param name="CookCount">How often the caller has made it.</param>
/// <param name="LastCookedAt">
/// When the caller last made it, or null if they never have. Read in the same
/// scan as the count, which the cook log's index already serves.
/// </param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="MatchedIngredients">How many named ingredients it uses.</param>
/// <param name="IngredientCount">How many ingredients it has in total.</param>
/// <param name="AddedToCookbookAt">
/// When it went on the cookbook being read, or null when none is.
/// </param>
public sealed record RecipeSearchRow(
    Guid RecipeId,
    string Title,
    Guid? ImageId,
    int? TotalMinutes,
    decimal YieldAmount,
    string YieldKind,
    IReadOnlyList<string> Tags,
    int CookCount,
    DateTimeOffset? LastCookedAt,
    DateTimeOffset UpdatedAt,
    int MatchedIngredients,
    int IngredientCount,
    DateTimeOffset? AddedToCookbookAt);
