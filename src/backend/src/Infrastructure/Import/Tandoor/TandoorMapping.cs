using System.Globalization;
using Domain.Import;

namespace Infrastructure.Import.Tandoor;

/// <summary>Reads Tandoor's vocabulary into this app's: the only file that knows what Tandoor calls anything.</summary>
/// <remarks>
/// Tandoor hangs ingredients off steps while this app keeps one list per recipe, so the steps are walked once
/// and both come out. The walk cannot be split: translating a step's template references needs to know where
/// that step's rows landed in the single list.
/// </remarks>
internal static class TandoorMapping
{
    internal static SourceRecipe ToSource(TandoorRecipeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new SourceRecipe
        {
            ExternalId = summary.Id.ToString(CultureInfo.InvariantCulture),
            Title = summary.Name ?? string.Empty,
            Description = summary.Description,
            ImageUrl = summary.Image,
            Servings = summary.Servings,
            PrepMinutes = summary.WorkingTime,
            CookMinutes = summary.WaitingTime
        };
    }

    internal static SourceRecipe ToSource(TandoorRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var contents = ToContents(recipe.Steps ?? []);

        return new SourceRecipe
        {
            ExternalId = recipe.Id.ToString(CultureInfo.InvariantCulture),
            Title = recipe.Name ?? string.Empty,
            Description = recipe.Description,
            SourceUrl = recipe.SourceUrl,
            ImageUrl = recipe.Image,
            Servings = recipe.Servings,
            PrepMinutes = recipe.WorkingTime,
            // Tandoor's "waiting time" (proving, resting, oven) is what this app calls cooking minutes.
            CookMinutes = recipe.WaitingTime,
            Tags = ToTags(recipe.Keywords),
            Groups = contents.Groups,
            Steps = contents.Steps
        };
    }

    private static IReadOnlyList<string> ToTags(IReadOnlyList<TandoorKeyword>? keywords) =>
    [
        .. (keywords ?? [])
            // The leaf name, not the label: nested keywords label as "Cuisine > Italian".
            .Select(keyword => keyword.Name ?? keyword.Label)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
    ];

    // A header step with ingredients under it becomes an ingredient group; a group is named only when there
    // is more than one, else "Zubereitung" would head the whole list. The running position counts ingredients
    // as this app will have them, while a step's template index counts Tandoor's per-step rows including
    // headers, so rows a step did not keep are still recorded.
    private static (IReadOnlyList<SourceIngredientGroup> Groups, IReadOnlyList<SourceStep> Steps)
        ToContents(IReadOnlyList<TandoorStep> steps)
    {
        List<SourceIngredientGroup> groups = [];
        List<SourceStep> instructions = [];
        var position = 0;

        foreach (var step in steps)
        {
            List<SourceIngredient> ingredients = [];
            List<TandoorStepIngredient> rows = [];

            foreach (var line in step.Ingredients ?? [])
            {
                // A header row is Tandoor's other way of writing a group, not an ingredient.
                var ingredient = line.IsHeader ? null : ToIngredient(line);

                rows.Add(new TandoorStepIngredient(line, ingredient is null ? null : position));

                if (ingredient is null)
                {
                    continue;
                }

                ingredients.Add(ingredient);
                position++;
            }

            if (ingredients.Count > 0)
            {
                groups.Add(new SourceIngredientGroup(step.Name?.Trim(), ingredients));
            }

            var segments = Words(step, rows);

            if (segments.Count == 0)
            {
                continue;
            }

            // Tandoor's step time is in minutes; this app uses seconds.
            instructions.Add(new SourceStep(segments, step.Time is > 0 ? step.Time * 60 : null));
        }

        return (
            groups.Count == 1 ? [new SourceIngredientGroup(null, groups[0].Ingredients)] : groups,
            instructions);
    }

    private static SourceIngredient? ToIngredient(TandoorIngredient line)
    {
        // The food is the noun: without one there is nothing to shop for, and Tandoor keeps such rows as free text.
        var name = line.Food?.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = line.OriginalText?.Trim();
        }

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // "no amount" means "salt", not "200 g salt": honoured, since a kept 1 would become "1 salt".
        var amount = line.NoAmount ? null : line.Amount;

        return new SourceIngredient(amount, line.Unit?.Name?.Trim(), name, line.Note?.Trim());
    }

    // A header step with no instruction vanishes (its ingredients were already taken), as does one whose
    // template resolved to nothing.
    private static IReadOnlyList<SourceStepSegment> Words(
        TandoorStep step,
        IReadOnlyList<TandoorStepIngredient> rows)
    {
        var instruction = TandoorTemplate.Read(step.Instruction, rows);
        var name = step.Name?.Trim();

        if (instruction.Count == 0)
        {
            return step.ShowAsHeader || string.IsNullOrEmpty(name)
                ? []
                : [new SourceTextSegment(name)];
        }

        return string.IsNullOrEmpty(name) || step.ShowAsHeader
            ? instruction
            : [new SourceTextSegment($"{name}: "), .. instruction];
    }
}
