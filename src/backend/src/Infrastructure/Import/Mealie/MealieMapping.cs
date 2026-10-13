using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Import;

namespace Infrastructure.Import.Mealie;

/// <summary>Reads Mealie's vocabulary into this app's: the only file that knows what Mealie calls anything.</summary>
/// <remarks>
/// Mealie already keeps one ingredient list per recipe, with a section heading on the line it starts, so
/// groups are a single walk. Its times are free text, read as well as they can be and otherwise left out.
/// </remarks>
internal static partial class MealieMapping
{
    internal static SourceRecipe ToSource(MealieRecipeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new SourceRecipe
        {
            ExternalId = summary.Id?.ToString("D") ?? string.Empty,
            Title = summary.Name ?? string.Empty,
            Description = summary.Description,
            ImageUrl = Picture(summary),
            Servings = Servings(summary),
            PrepMinutes = Minutes(summary.PrepTime),
            CookMinutes = Minutes(summary.PerformTime) ?? Minutes(summary.CookTime)
        };
    }

    internal static SourceRecipe ToSource(MealieRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        return ToSource((MealieRecipeSummary)recipe) with
        {
            SourceUrl = recipe.OrgUrl,
            Tags = ToTags(recipe),
            Groups = ToGroups(recipe.RecipeIngredient ?? []),
            Steps = [.. (recipe.RecipeInstructions ?? []).Select(ToStep).OfType<SourceStep>()]
        };
    }

    /// <summary>Whole minutes in a time as Mealie keeps it, or null when it does not say.</summary>
    /// <remarks>
    /// ISO 8601 from older scrapes, "1 hour 30 minutes" or "1 Stunde 30 Minuten" from newer ones, and
    /// whatever was typed: a bare number is minutes, as Mealie's own form suggests.
    /// </remarks>
    internal static int? Minutes(string? written)
    {
        var text = written?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith('P'))
        {
            return XmlDuration.ToMinutes(text);
        }

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var bare))
        {
            return Within(bare);
        }

        var total = 0m;

        foreach (Match part in TimePart().Matches(text))
        {
            var amount = decimal.Parse(
                part.Groups["amount"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture);

            total += part.Groups["hours"].Success ? amount * 60 : amount;
        }

        return Within((int)Math.Round(total));
    }

    // Under a day, as for any other import: a recipe that says otherwise has misread its own time.
    private static int? Within(int minutes) => minutes is > 0 and < 24 * 60 ? minutes : null;

    [GeneratedRegex(
        @"(?<amount>\d+(?:[.,]\d+)?)\s*(?:(?<hours>stunden|stunde|std|hours|hour|hrs|hr|h)|minuten|minutes|minute|mins|min|m)(?![a-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimePart();

    private static decimal? Servings(MealieRecipeSummary summary) =>
        summary.RecipeServings is > 0
            ? summary.RecipeServings
            : YieldText.Read(summary.RecipeYield)?.Amount;

    // Mealie serves every recipe's picture at a fixed path; the image field only says whether there is one.
    private static string? Picture(MealieRecipeSummary summary) =>
        summary.Id is { } id && !string.IsNullOrWhiteSpace(summary.Image)
            ? $"/api/media/recipes/{id:D}/images/original.webp"
            : null;

    // Categories are tags too here: this app has one way of sorting recipes by word.
    private static IReadOnlyList<string> ToTags(MealieRecipe recipe) =>
    [
        .. (recipe.Tags ?? [])
            .Concat(recipe.RecipeCategory ?? [])
            .Select(named => named.Name?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    // A heading starts a group at its own line; a group is named only when there is more than one, as for Tandoor.
    private static List<SourceIngredientGroup> ToGroups(IReadOnlyList<MealieIngredient> lines)
    {
        List<SourceIngredientGroup> groups = [];
        string? heading = null;
        List<SourceIngredient> ingredients = [];

        foreach (var line in lines)
        {
            var title = line.Title?.Trim();

            if (!string.IsNullOrEmpty(title))
            {
                Close();
                heading = title;
            }

            if (ToIngredient(line) is { } ingredient)
            {
                ingredients.Add(ingredient);
            }
        }

        Close();

        return groups.Count == 1 ? [new SourceIngredientGroup(null, groups[0].Ingredients)] : groups;

        void Close()
        {
            if (ingredients.Count > 0)
            {
                groups.Add(new SourceIngredientGroup(heading, ingredients));
            }

            ingredients = [];
        }
    }

    private static SourceIngredient? ToIngredient(MealieIngredient line)
    {
        var food = line.Food?.Name?.Trim();

        // An unparsed line: its words are the ingredient, and its quantity is a default nobody wrote.
        if (string.IsNullOrEmpty(food))
        {
            var text = new[] { line.Note, line.OriginalText, line.Display }
                .Select(value => value?.Trim())
                .FirstOrDefault(value => !string.IsNullOrEmpty(value));

            return text is null ? null : new SourceIngredient(null, null, text, null);
        }

        var note = line.Note?.Trim();

        return new SourceIngredient(
            line.Quantity is > 0 ? line.Quantity : null,
            line.Unit?.Name?.Trim() is { Length: > 0 } unit ? unit : null,
            food,
            string.IsNullOrEmpty(note) ? null : note);
    }

    // A heading leads its step as Tandoor's step names do; a heading with no words under it stands alone.
    private static SourceStep? ToStep(MealieStep step)
    {
        var text = step.Text?.Trim();
        var title = step.Title?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return string.IsNullOrEmpty(title) ? null : new SourceStep(title, null);
        }

        return new SourceStep(string.IsNullOrEmpty(title) ? text : $"{title}: {text}", null);
    }
}
