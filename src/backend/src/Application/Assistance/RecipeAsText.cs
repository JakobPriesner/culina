using System.Globalization;
using System.Text;
using Domain.Recipes;

namespace Application.Assistance;

/// <summary>
/// Writes a stored recipe out as plain text, to be read back to a model.
/// </summary>
/// <remarks>
/// <para>
/// Text rather than the JSON the rest of the app moves recipes around as, and
/// the reason is the one thing this file is really about: what goes in here is
/// <em>material</em>, not instruction. JSON with field names in it reads, to a
/// model, uncomfortably like the structure it was also handed in the schema —
/// and the closer the untrusted half looks to the trusted half, the more room
/// there is for the untrusted half to be mistaken for it.
/// </para>
/// <para>
/// Ids are left out for the same reason, and for a second one: the draft that
/// comes back has no ids either, so anything the model repeated back would be
/// noise the mapper had to ignore.
/// </para>
/// </remarks>
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
            // Built once: a step names its ingredients by id, and a sentence
            // read back with the names missing would be a sentence the model
            // was asked to improve without being shown half of it.
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

    /// <summary>
    /// One step, with its ingredient references written back as plain words.
    /// </summary>
    /// <remarks>
    /// A step is stored as segments so the app can scale the amounts inside a
    /// sentence. A model has no use for that: it is being shown the sentence.
    /// </remarks>
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

    /// <summary>
    /// A number without a trailing run of zeroes.
    /// </summary>
    /// <remarks>
    /// "2" rather than "2.000". Invariant, because this is being read by a
    /// model rather than by the person whose locale it is.
    /// </remarks>
    private static string Number(decimal value) =>
        value.Normalize().ToString(CultureInfo.InvariantCulture);

    private static decimal Normalize(this decimal value) => value / 1.000000000000000000000000000000000m;
}
