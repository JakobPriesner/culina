using Contracts.Recipes.Sources;
using Domain.Import;

namespace Application.Recipes.Sources;

/// <summary>
/// Turns connections and other people's recipes into what the wire carries.
/// </summary>
/// <remarks>
/// Hand-written, like every other mapper here, and with one rule that is not
/// cosmetic: <see cref="RecipeSource.Secret"/> has no line in this file, and
/// must never get one. A token that is returned once is a token in a browser's
/// network log, in a client's cache, and in whatever a support screenshot
/// happens to catch.
/// </remarks>
internal static class SourceMappings
{
    internal static SourceSummary ToSummary(this RecipeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SourceSummary
        {
            SourceId = source.Id,
            Kind = source.Kind.Code,
            Label = source.Label,
            Address = source.Address.Value,
            CreatedAt = source.CreatedAt,
            LastUsedAt = source.LastUsedAt
        };
    }

    internal static SourceRecipesResponse ToResponse(
        this SourcePage page,
        IReadOnlyDictionary<string, Guid> alreadyHere)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(alreadyHere);

        return new SourceRecipesResponse
        {
            Items = [.. page.Recipes.Select(recipe => recipe.ToSummary(alreadyHere))],
            NextPage = page.NextPage,
            Total = page.Total
        };
    }

    private static SourceRecipeSummary ToSummary(
        this SourceRecipe recipe,
        IReadOnlyDictionary<string, Guid> alreadyHere) =>
        new()
        {
            ExternalId = recipe.ExternalId,
            Title = recipe.Title,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            TotalMinutes = recipe.PrepMinutes is null && recipe.CookMinutes is null
                ? null
                : (recipe.PrepMinutes ?? 0) + (recipe.CookMinutes ?? 0),
            AlreadyHere = alreadyHere.TryGetValue(recipe.ExternalId, out var mine) ? mine : null
        };
}
