using System.Globalization;
using Domain.Import;

namespace Infrastructure.Import.Tandoor;

/// <summary>
/// Reads Tandoor's vocabulary into this app's.
/// </summary>
/// <remarks>
/// <para>
/// The anti-corruption layer, and the only file in the codebase that knows what
/// Tandoor calls anything. Everything past it works in
/// <see cref="SourceRecipe"/>, which is what makes adding Mealie a sibling of
/// this file rather than a change to the import.
/// </para>
/// <para>
/// The one real structural difference is where ingredients live. Tandoor hangs
/// them off steps; this app keeps one list for the recipe and lets a step point
/// into it. So the steps are walked once, and two things come out: the
/// instructions, and the ingredient groups they were carrying.
/// </para>
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

        var steps = recipe.Steps ?? [];

        return new SourceRecipe
        {
            ExternalId = recipe.Id.ToString(CultureInfo.InvariantCulture),
            Title = recipe.Name ?? string.Empty,
            Description = recipe.Description,
            SourceUrl = recipe.SourceUrl,
            ImageUrl = recipe.Image,
            Servings = recipe.Servings,
            PrepMinutes = recipe.WorkingTime,
            // Tandoor's "waiting time" is proving, resting and oven time: the
            // part of a recipe you are not standing over it for, which is what
            // this app means by cooking minutes.
            CookMinutes = recipe.WaitingTime,
            Tags = ToTags(recipe.Keywords),
            Groups = ToGroups(steps),
            Steps = ToSteps(steps)
        };
    }

    private static IReadOnlyList<string> ToTags(IReadOnlyList<TandoorKeyword>? keywords) =>
    [
        .. (keywords ?? [])
            // The leaf name, not the label: on an instance that nests keywords
            // the label is "Cuisine > Italian", and a tag with a path in it is
            // a tag nobody types.
            .Select(keyword => keyword.Name ?? keyword.Label)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
    ];

    /// <summary>
    /// The ingredients, regrouped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Tandoor step that is a header ("For the sauce:") with ingredients
    /// under it is precisely this app's ingredient group, so it becomes one. A
    /// recipe that never uses headers — most of them — has every ingredient on
    /// step one, and comes out as a single unnamed list, which is what it was.
    /// </para>
    /// <para>
    /// A group is named only when there is more than one. Otherwise the first
    /// step's title, which is often just "Zubereitung", would become a heading
    /// over the whole ingredient list.
    /// </para>
    /// </remarks>
    private static List<SourceIngredientGroup> ToGroups(IReadOnlyList<TandoorStep> steps)
    {
        List<SourceIngredientGroup> groups = [];

        foreach (var step in steps)
        {
            var ingredients = (step.Ingredients ?? [])
                // A header row inside the list is Tandoor's other way of
                // writing a group, and it is not an ingredient.
                .Where(line => !line.IsHeader)
                .Select(ToIngredient)
                .Where(line => line is not null)
                .Select(line => line!)
                .ToList();

            if (ingredients.Count > 0)
            {
                groups.Add(new SourceIngredientGroup(step.Name?.Trim(), ingredients));
            }
        }

        return groups.Count == 1
            ? [new SourceIngredientGroup(null, groups[0].Ingredients)]
            : groups;
    }

    private static SourceIngredient? ToIngredient(TandoorIngredient line)
    {
        // The food is the noun. Without one there is nothing to shop for and
        // nothing to scale, and Tandoor keeps such rows as free text.
        var name = line.Food?.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = line.OriginalText?.Trim();
        }

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // "no amount" is Tandoor being told this one is "salt", not "200 g
        // salt". Honoured rather than overridden, because a 1 it kept around
        // internally would become "1 salt" here.
        var amount = line.NoAmount ? null : line.Amount;

        return new SourceIngredient(amount, line.Unit?.Name?.Trim(), name, line.Note?.Trim());
    }

    private static List<SourceStep> ToSteps(IReadOnlyList<TandoorStep> steps)
    {
        List<SourceStep> instructions = [];

        foreach (var step in steps)
        {
            var text = Words(step);

            if (text is null)
            {
                continue;
            }

            // Tandoor counts a step's time in minutes; this app counts it in
            // seconds, because a step can be "rest 30 seconds".
            instructions.Add(new SourceStep(text, step.Time is > 0 ? step.Time * 60 : null));
        }

        return instructions;
    }

    /// <summary>
    /// What a step says, with its title folded in when it has one.
    /// </summary>
    /// <remarks>
    /// A header step with no instruction carried only its ingredients, which
    /// have already been taken, so it disappears rather than becoming an empty
    /// step called "For the sauce".
    /// </remarks>
    private static string? Words(TandoorStep step)
    {
        var instruction = step.Instruction?.Trim();
        var name = step.Name?.Trim();

        if (string.IsNullOrEmpty(instruction))
        {
            return step.ShowAsHeader || string.IsNullOrEmpty(name) ? null : name;
        }

        return string.IsNullOrEmpty(name) || step.ShowAsHeader
            ? instruction
            : $"{name}: {instruction}";
    }
}
