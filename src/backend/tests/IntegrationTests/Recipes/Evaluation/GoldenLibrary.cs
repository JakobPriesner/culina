using System.Reflection;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes.Evaluation;

/// <summary>
/// The sixty recipes and forty judged queries of <c>golden-library.json</c>.
/// </summary>
/// <remarks>
/// A library built to be difficult — see <see cref="SearchEvaluationTests"/> —
/// and shared, because every feature that reads the search document is
/// measured against the same kitchen.
/// </remarks>
internal sealed record GoldenLibrary(List<GoldenRecipe> Recipes, List<GoldenQuery> Queries)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static GoldenLibrary Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("golden-library.json")!;

        return JsonSerializer.Deserialize<GoldenLibrary>(stream, Options)!;
    }

    /// <summary>Writes every recipe into the kitchen, and says which id each title got.</summary>
    internal async Task<Dictionary<string, Guid>> SeedAsync(Kitchen kitchen)
    {
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var recipe in Recipes)
        {
            ids[recipe.Title] = await kitchen.SaveAsync(
                recipe.Title,
                recipe.Language,
                recipe.Prep,
                recipe.Cook,
                [.. recipe.Ingredients.Select(name => (name, (string?)null))],
                [.. recipe.Tags],
                recipe.Step);
        }

        return ids;
    }
}

internal sealed record GoldenRecipe(
    string Title,
    string Language,
    int? Prep,
    int? Cook,
    List<string> Tags,
    List<string> Ingredients,
    string Step);

internal sealed record GoldenQuery(
    string Query,
    string Class,
    Dictionary<string, int> Grades,
    bool Empty,
    List<string> Never,
    List<string>? Chips);
