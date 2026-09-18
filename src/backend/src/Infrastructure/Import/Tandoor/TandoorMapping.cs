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
/// <para>
/// Which is also why the walk cannot be split in two. A step's instruction may
/// refer to its own ingredients by position — see
/// <see cref="TandoorTemplate"/> — and translating those references means
/// knowing, at the moment the instruction is read, where each of that step's
/// rows has landed in the recipe's single list. Counting it twice would be two
/// places to get the same off-by-one wrong.
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
            // Tandoor's "waiting time" is proving, resting and oven time: the
            // part of a recipe you are not standing over it for, which is what
            // this app means by cooking minutes.
            CookMinutes = recipe.WaitingTime,
            Tags = ToTags(recipe.Keywords),
            Groups = contents.Groups,
            Steps = contents.Steps
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
    /// The ingredients and the instructions, from one walk of the steps.
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
    /// <para>
    /// The running <c>position</c> is what the instructions are read against.
    /// It counts ingredients as this app will have them — one list for the
    /// whole recipe — while the index a step's template uses counts rows as
    /// Tandoor sent them, per step and including the headers. Holding both at
    /// once is the whole job, and it is why the rows a step did not keep are
    /// still recorded rather than filtered away.
    /// </para>
    /// </remarks>
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
                // A header row inside the list is Tandoor's other way of
                // writing a group, and it is not an ingredient.
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

            // Tandoor counts a step's time in minutes; this app counts it in
            // seconds, because a step can be "rest 30 seconds".
            instructions.Add(new SourceStep(segments, step.Time is > 0 ? step.Time * 60 : null));
        }

        return (
            groups.Count == 1 ? [new SourceIngredientGroup(null, groups[0].Ingredients)] : groups,
            instructions);
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

    /// <summary>
    /// What a step says, with its templates resolved and its title folded in.
    /// </summary>
    /// <remarks>
    /// A header step with no instruction carried only its ingredients, which
    /// have already been taken, so it disappears rather than becoming an empty
    /// step called "For the sauce". A step whose instruction was nothing but a
    /// template that resolved to nothing disappears by the same rule.
    /// </remarks>
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
