using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Infrastructure.Import.Culina;

/// <summary>Reads another Culina's recipe into the one shape every import goes through.</summary>
/// <remarks>
/// Nearly one to one, since it is the same model: ingredient ids become positions in the recipe's ingredients read
/// group by group, which is how <see cref="SourceRecipe.Ingredients"/> counts, so references and uses land on the
/// same lines here. Personal notes and the cook log stay behind, as they do for every other app.
/// </remarks>
internal static class CulinaMapping
{
    /// <summary>Its picture, at the widest size the other instance serves.</summary>
    internal static string Picture(Guid recipeId) => $"/api/v1/recipes/{recipeId:D}/image?w=1600";

    internal static SourceRecipe ToSource(CulinaRecipeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new SourceRecipe
        {
            ExternalId = summary.RecipeId?.ToString("D") ?? string.Empty,
            Title = summary.Title ?? string.Empty,
            ImageUrl = summary.RecipeId is { } id && summary.ImageId is not null ? Picture(id) : null,
            // The list carries only the total; it is shown while choosing, and the detail splits it again.
            CookMinutes = summary.TotalMinutes
        };
    }

    internal static SourceRecipe ToSource(CulinaRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var groups = recipe.Groups ?? [];
        var positions = groups
            .SelectMany(group => group.Ingredients ?? [])
            .Select((line, index) => (line.IngredientId, index))
            .Where(entry => entry.IngredientId is not null)
            .GroupBy(entry => entry.IngredientId!.Value)
            .ToDictionary(found => found.Key, found => found.First().index);

        return new SourceRecipe
        {
            ExternalId = recipe.RecipeId?.ToString("D") ?? string.Empty,
            Title = recipe.Title ?? string.Empty,
            Description = recipe.Description,
            SourceUrl = recipe.Origin?.SourceUrl,
            ImageUrl = recipe.RecipeId is { } id && recipe.ImageId is not null ? Picture(id) : null,
            Servings = recipe.YieldAmount,
            YieldKind = recipe.YieldKind switch
            {
                "pieces" => YieldKind.Pieces,
                "servings" => YieldKind.Servings,
                _ => null
            },
            YieldLabel = recipe.YieldLabel,
            Language = recipe.Language switch
            {
                "en" => Language.En,
                "de" => Language.De,
                _ => null
            },
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            Tags = recipe.Tags ?? [],
            // Every line is kept, even a nameless one, so the positions above still count from the same start.
            Groups =
            [
                .. groups.Select(group => new SourceIngredientGroup(
                    group.Name,
                    [.. (group.Ingredients ?? []).Select(line =>
                        new SourceIngredient(line.Quantity, line.Unit, line.Name ?? string.Empty, line.Note))]))
            ],
            Steps = [.. (recipe.Steps ?? []).Select(step => ToStep(step, positions))]
        };
    }

    private static SourceStep ToStep(CulinaStep step, Dictionary<Guid, int> positions) =>
        new(
            [.. (step.Segments ?? []).Select(segment => ToSegment(segment, positions)).OfType<SourceStepSegment>()],
            step.DurationSeconds)
        {
            Title = step.Title,
            Uses =
            [
                .. (step.Uses ?? [])
                    .Where(positions.ContainsKey)
                    .Select(used => positions[used])
            ]
        };

    // A reference to a line that is not in the recipe is dropped, not guessed: a step pointing at the wrong
    // ingredient is the failure this app exists to prevent.
    private static SourceStepSegment? ToSegment(CulinaSegment segment, Dictionary<Guid, int> positions) =>
        segment.Type switch
        {
            "ingredient" when segment.RecipeIngredientId is { } id && positions.TryGetValue(id, out var position) =>
                new SourceIngredientReference(position),
            "text" when !string.IsNullOrEmpty(segment.Value) => new SourceTextSegment(segment.Value),
            _ => null
        };
}
