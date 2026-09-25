using Contracts.Shopping;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Maps a list onto the shape the API returns.</summary>
internal static class ShoppingMappings
{
    internal static Response Describe(this ShoppingList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return new Response
        {
            ListId = list.Id,
            // Shop order, so a list read top to bottom is a route.
            Items = [.. list.Items
                .OrderBy(item => item.Section)
                .ThenBy(item => item.SortOrder)
                .Select(item => new ItemContract
                {
                    ItemId = item.Id,
                    Name = item.Name.Value,
                    Quantity = item.Quantity.Amount,
                    Unit = Recipes.RecipeWords.Of(item.Quantity.Unit),
                    Section = ShoppingWords.Of(item.Section),
                    IsChecked = item.IsChecked,
                    IsManual = item.IsManual,
                    Sources = [.. item.Sources.Select(source => new SourceContract
                    {
                        RecipeId = source.RecipeId,
                        RecipeTitle = source.RecipeTitle,
                        Quantity = source.Quantity.Amount,
                        Unit = Recipes.RecipeWords.Of(source.Quantity.Unit),
                        PlannedDate = source.PlannedDate,
                        PlannedSlot = source.PlannedSlot is { } slot
                            ? Planning.PlanningWords.Of(slot)
                            : null
                    })]
                })],
            Version = list.Version
        };
    }
}
