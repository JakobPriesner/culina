namespace Application.Abstractions;

/// <summary>What to look for.</summary>
/// <param name="HouseholdId">Whose recipes.</param>
/// <param name="UserId">Who is asking, for their own cook counts.</param>
/// <param name="Query">Free text over title, description and ingredient names.</param>
/// <param name="Tags">Tag slugs, all of which must be present.</param>
/// <param name="Ingredients">Ingredients the caller has, for ranking.</param>
/// <param name="MaxMinutes">A ceiling on total time.</param>
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
    Relevance = 4
}

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
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="MatchedIngredients">How many named ingredients it uses.</param>
/// <param name="IngredientCount">How many ingredients it has in total.</param>
public sealed record RecipeSearchRow(
    Guid RecipeId,
    string Title,
    Guid? ImageId,
    int? TotalMinutes,
    decimal YieldAmount,
    string YieldKind,
    IReadOnlyList<string> Tags,
    int CookCount,
    DateTimeOffset UpdatedAt,
    int MatchedIngredients,
    int IngredientCount);
