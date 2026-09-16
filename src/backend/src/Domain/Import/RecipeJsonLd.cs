using System.Globalization;
using System.Text.Json;
using Domain.Recipes;

namespace Domain.Import;

/// <summary>
/// Reads a schema.org Recipe out of a page's JSON-LD.
/// </summary>
/// <remarks>
/// <para>
/// Almost every recipe site publishes this, because search engines read it.
/// That makes it the honest way to import a recipe: it is the structured data
/// the site chose to publish, rather than a guess at what its markup means.
/// </para>
/// <para>
/// What comes out is a <em>draft</em>, and it is shown back for correction
/// before anything is saved. A wrong reading you cannot see is worse than no
/// reading at all, and this one has to tolerate a decade of half-correct
/// implementations: the block may be an array, it may be a <c>@graph</c>, the
/// type may be a string or a list of them, and an instruction may be a string,
/// a <c>HowToStep</c>, or a <c>HowToSection</c> with steps inside it.
/// </para>
/// </remarks>
public static class RecipeJsonLd
{
    /// <summary>What a page said about a recipe.</summary>
    /// <param name="Title">Its name, if it gave one.</param>
    /// <param name="IngredientLines">Its ingredients, one line each, as written.</param>
    /// <param name="Steps">Its instructions, one paragraph each.</param>
    /// <param name="Servings">What it says it makes, if it is a plain number.</param>
    /// <param name="TotalMinutes">How long it takes, if it said.</param>
    public sealed record Draft(
        string? Title,
        IReadOnlyList<string> IngredientLines,
        IReadOnlyList<string> Steps,
        decimal? Servings,
        int? TotalMinutes);

    /// <summary>The recipe a page publishes, or null when it publishes none.</summary>
    /// <param name="json">One JSON-LD block's contents.</param>
    public static Draft? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            var recipe = FindRecipe(document.RootElement);

            return recipe is null ? null : ToDraft(recipe.Value);
        }
        catch (JsonException)
        {
            // A page with broken JSON-LD is a page with no JSON-LD. Nothing is
            // lost: the caller falls back to reading the words.
            return null;
        }
    }

    /// <summary>
    /// The first Recipe anywhere in the block.
    /// </summary>
    /// <remarks>
    /// Depth-first through arrays and <c>@graph</c>, which is how the same
    /// recipe arrives wrapped in a WebPage, an Article and an Organisation
    /// depending on which plugin published it.
    /// </remarks>
    private static JsonElement? FindRecipe(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindRecipe(item) is { } found)
                    {
                        return found;
                    }
                }

                return null;

            case JsonValueKind.Object:
                if (IsRecipe(element))
                {
                    return element;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object
                        && FindRecipe(property.Value) is { } nested)
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    private static bool IsRecipe(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => IsRecipeName(type.GetString()),
            JsonValueKind.Array => type.EnumerateArray()
                .Any(one => one.ValueKind == JsonValueKind.String && IsRecipeName(one.GetString())),
            _ => false
        };
    }

    private static bool IsRecipeName(string? type) =>
        string.Equals(type, "Recipe", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "schema:Recipe", StringComparison.OrdinalIgnoreCase);

    private static Draft ToDraft(JsonElement recipe) => new(
        Text(recipe, "name"),
        [.. Strings(recipe, "recipeIngredient").Concat(Strings(recipe, "ingredients"))],
        [.. Instructions(recipe)],
        Servings(recipe),
        Minutes(recipe));

    /// <summary>A string property, however the site chose to wrap it.</summary>
    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return Flatten(value).FirstOrDefault();
    }

    private static IEnumerable<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? Flatten(value) : [];

    /// <summary>
    /// Whatever this is, as the lines of text a person would read.
    /// </summary>
    /// <remarks>
    /// A string, a list of them, or an object with a <c>text</c> or
    /// <c>name</c> — the three shapes a decade of plugins settled on for the
    /// same idea.
    /// </remarks>
    private static IEnumerable<string> Flatten(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = Tidy(value.GetString());

                if (text is not null)
                {
                    yield return text;
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    foreach (var line in Flatten(item))
                    {
                        yield return line;
                    }
                }

                break;

            case JsonValueKind.Object:
                // A HowToSection carries its steps; a HowToStep carries its
                // words. Taking the section's own name as a step would put
                // "For the sauce" in the method as an instruction.
                if (value.TryGetProperty("itemListElement", out var inner))
                {
                    foreach (var line in Flatten(inner))
                    {
                        yield return line;
                    }

                    break;
                }

                foreach (var property in new[] { "text", "name" })
                {
                    if (value.TryGetProperty(property, out var written)
                        && Tidy(written.ValueKind == JsonValueKind.String ? written.GetString() : null)
                            is { } line)
                    {
                        yield return line;

                        break;
                    }
                }

                break;

            default:
                break;
        }
    }

    private static IEnumerable<string> Instructions(JsonElement recipe) =>
        Strings(recipe, "recipeInstructions");

    /// <summary>
    /// What it makes, when that is a number.
    /// </summary>
    /// <remarks>
    /// "4", "4 servings" and "4-6" all appear. The first number wins and a
    /// range takes its lower bound, which is the same reading the paste import
    /// gives — and anything with no number at all is left alone rather than
    /// guessed at.
    /// </remarks>
    private static decimal? Servings(JsonElement recipe)
    {
        var written = Strings(recipe, "recipeYield").FirstOrDefault()
            ?? Strings(recipe, "yield").FirstOrDefault();

        if (written is null)
        {
            return null;
        }

        // Skipped to the first digit rather than read from the start: "Serves 4"
        // and "Makes 12" are as ordinary as "4 servings", and taking only
        // leading digits read them as no yield at all.
        var digits = new string([
            .. written.SkipWhile(one => !char.IsDigit(one))
                .TakeWhile(one => char.IsDigit(one) || one == '.')
        ]);

        return decimal.TryParse(digits, CultureInfo.InvariantCulture, out var amount)
            && amount > 0
            && amount <= Yield.MaxAmount
                ? amount
                : null;
    }

    /// <summary>How long it takes, from an ISO 8601 duration.</summary>
    private static int? Minutes(JsonElement recipe)
    {
        var written = Strings(recipe, "totalTime").FirstOrDefault();

        if (written is null)
        {
            return null;
        }

        return XmlDuration.ToMinutes(written);
    }

    /// <summary>Collapses whitespace and drops what is left if it is nothing.</summary>
    private static string? Tidy(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var collapsed = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length == 0 ? null : collapsed;
    }
}

/// <summary>Reads the <c>PT30M</c> durations schema.org uses.</summary>
internal static class XmlDuration
{
    internal static int? ToMinutes(string value)
    {
        try
        {
            var span = System.Xml.XmlConvert.ToTimeSpan(value);

            return span > TimeSpan.Zero && span < TimeSpan.FromDays(1)
                ? (int)span.TotalMinutes
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
