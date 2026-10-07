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
        CoverRecipeIds = [.. shelf.Cover.Select(picture => picture.RecipeId)],
        CoverPictures = [.. shelf.Cover.Select(ToContract)],
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
        CoverRecipeIds = [.. shelf.Cover.Select(picture => picture.RecipeId)],
        CoverPictures = [.. shelf.Cover.Select(ToContract)],
        CreatedBy = shelf.Cookbook.CreatedBy,
        CreatedAt = shelf.Cookbook.CreatedAt,
        UpdatedAt = shelf.Cookbook.UpdatedAt,
        Version = shelf.Cookbook.Version
    };

    private static CookbookCoverPicture ToContract(CoverPicture picture) => new()
    {
        RecipeId = picture.RecipeId,
        ImageId = picture.ImageId
    };

    // Null for a manual cookbook: "no rules" and "blank rules" are different claims.
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
