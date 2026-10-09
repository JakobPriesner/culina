using Domain.Recipes;

namespace Application.Recipes;

/// <summary>
/// Shortens text from sources the person cannot correct (an import, an archive, a model's draft) to
/// what a recipe holds, so one long description does not cost the whole recipe.
/// </summary>
internal static class RecipeFit
{
    internal static string? Description(string? value) => Shorten(value, Recipe.MaxDescriptionLength);

    internal static IReadOnlyList<string> Tags(IEnumerable<string?>? tags) =>
    [
        .. (tags ?? [])
            .Select(tag => Shorten(tag, Recipe.MaxTagLength))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Recipe.MaxTags)
    ];

    /// <summary>
    /// Shortens rather than refuses, cutting at a word boundary near the end; null for nothing.
    /// </summary>
    internal static string? Shorten(string? value, int limit)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length <= limit)
        {
            return trimmed;
        }

        var cut = trimmed[..limit];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > limit - 20 ? cut[..lastSpace] : cut).TrimEnd();
    }
}
