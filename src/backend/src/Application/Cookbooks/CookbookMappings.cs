using Application.Abstractions;
using Contracts.Cookbooks;
using Domain.Cookbooks;

namespace Application.Cookbooks;

/// <summary>Turns cookbooks into what the wire carries.</summary>
internal static class CookbookMappings
{
    internal static CookbooksResponse ToResponse(this CookbookPage page) => new()
    {
        Items = [.. page.Items.Select(ToSummary)],
        NextCursor = page.NextCursor,
        Total = page.Total
    };

    internal static CookbookSummary ToSummary(this CookbookOnAShelf shelf) => new()
    {
        CookbookId = shelf.Cookbook.Id,
        Name = shelf.Cookbook.Name.Value,
        Description = shelf.Cookbook.Description,
        Kind = CookbookWords.Of(shelf.Cookbook.Kind),
        Rules = ToContract(shelf.Cookbook),
        RecipeCount = shelf.RecipeCount,
        CoverRecipeIds = shelf.CoverRecipeIds,
        UpdatedAt = shelf.Cookbook.UpdatedAt
    };

    internal static CookbookDetail ToDetail(this CookbookOnAShelf shelf) => new()
    {
        CookbookId = shelf.Cookbook.Id,
        HouseholdId = shelf.Cookbook.HouseholdId,
        Name = shelf.Cookbook.Name.Value,
        Description = shelf.Cookbook.Description,
        Kind = CookbookWords.Of(shelf.Cookbook.Kind),
        Rules = ToContract(shelf.Cookbook),
        RecipeCount = shelf.RecipeCount,
        CoverRecipeIds = shelf.CoverRecipeIds,
        CreatedBy = shelf.Cookbook.CreatedBy,
        CreatedAt = shelf.Cookbook.CreatedAt,
        UpdatedAt = shelf.Cookbook.UpdatedAt,
        Version = shelf.Cookbook.Version
    };

    /// <summary>The rules, but only for a shelf that has any.</summary>
    /// <remarks>
    /// Null rather than an empty object for a manual cookbook: "this has no
    /// rules" and "this has rules, and they are blank" are different claims,
    /// and only one of them is possible.
    /// </remarks>
    private static CookbookRulesContract? ToContract(Cookbook cookbook) =>
        cookbook.Kind == CookbookKind.Smart
            ? new CookbookRulesContract
            {
                Tags = cookbook.Rules.Tags,
                Ingredients = cookbook.Rules.Ingredients,
                MaxMinutes = cookbook.Rules.MaxMinutes
            }
            : null;

    internal static RecipeCookbooksResponse ToResponse(this IReadOnlyList<CookbookOnAShelf> shelves) => new()
    {
        Items =
        [
            .. shelves.Select(shelf => new RecipeCookbook
            {
                CookbookId = shelf.Cookbook.Id,
                Name = shelf.Cookbook.Name.Value
            })
        ]
    };
}
