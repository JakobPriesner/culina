using System.Globalization;
using System.Text;
using Domain.Recipes;

namespace Application.Assistance;

/// <summary>
/// Writes a stored recipe as plain text for a model. Text, not JSON, so untrusted material does not
/// resemble the trusted schema; ids are left out because the returned draft has none.
/// </summary>
internal static class RecipeAsText
{
    /// <summary>Renders it the way a person would write it down.</summary>
    /// <param name="recipe">The stored recipe.</param>
    internal static string Of(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var written = new StringBuilder();

        written.AppendLine(recipe.Title.Value);

        if (!string.IsNullOrWhiteSpace(recipe.Description))
        {
            written.AppendLine().AppendLine(recipe.Description);
        }

        Times(written, recipe);

        foreach (var group in recipe.Groups)
        {
            written.AppendLine();

            if (!string.IsNullOrWhiteSpace(group.Name))
            {
                written.AppendLine(group.Name);
            }

            foreach (var line in group.Ingredients)
            {
                written.AppendLine(Line(line));
            }
        }

        if (recipe.Steps.Count > 0)
        {
            // A step names its ingredients by id, so their names are needed to render it.
            var named = recipe.Ingredients.ToDictionary(line => line.Id, line => line.Name);

            written.AppendLine();

            foreach (var (step, number) in recipe.Steps.Select((step, index) => (step, index + 1)))
            {
                written.AppendLine(Step(step, number, named));
            }
        }

        return written.ToString().TrimEnd();
    }

    private static void Times(StringBuilder written, Recipe recipe)
    {
        written.AppendLine(
            CultureInfo.InvariantCulture,
            $"Makes: {Number(recipe.Yield.Amount)} "
            + $"{recipe.Yield.Label ?? recipe.Yield.Kind.ToString()}");

        if (recipe.PrepMinutes is { } prep)
        {
            written.AppendLine(CultureInfo.InvariantCulture, $"Preparation: {prep} minutes");
        }

        if (recipe.CookMinutes is { } cook)
        {
            written.AppendLine(CultureInfo.InvariantCulture, $"Cooking: {cook} minutes");
        }
    }

    private static string Line(RecipeIngredient line)
    {
        var amount = line.Quantity.Amount is { } value ? Number(value) : null;
        var unit = line.Quantity.Unit?.Code;
        var note = string.IsNullOrWhiteSpace(line.Note) ? null : $", {line.Note}";

        return $"- {string.Join(' ', new[] { amount, unit, line.Name }.OfType<string>())}{note}";
    }

    private static string Step(Step step, int number, IReadOnlyDictionary<Guid, string> named)
    {
        var title = string.IsNullOrWhiteSpace(step.Title) ? null : $" ({step.Title})";

        var words = string.Concat(step.Segments.Select(segment => segment switch
        {
            TextSegment text => text.Value,
            IngredientSegment mention => named.GetValueOrDefault(mention.RecipeIngredientId, string.Empty),
            _ => string.Empty
        }));

        return $"{number}.{title} {words}";
    }

    // Without trailing zeroes ("2", not "2.000"); invariant because a model reads it.
    private static string Number(decimal value) =>
        value.Normalize().ToString(CultureInfo.InvariantCulture);

    private static decimal Normalize(this decimal value) => value / 1.000000000000000000000000000000000m;
}
