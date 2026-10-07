using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Update;

/// <summary>
/// The ids a save may keep for its groups, lines and steps: the ones this
/// recipe already has.
/// </summary>
/// <remarks>
/// <para>
/// A client sends ids back so that a kept step keeps the notes hanging off it,
/// and so that a step can point at a line in the same request. An id this
/// recipe does not own cannot have come from it — it is another recipe's, or
/// made up — and it used to be inserted as sent. Another recipe's id then
/// failed on the primary key, which told the caller, by failing, that the id
/// exists somewhere.
/// </para>
/// <para>
/// Such an id is treated as what it is, a new row: it gets a fresh id, and
/// every step in the request that points at it follows it there. The request
/// still means what it said, and nothing outside this recipe is touched or
/// revealed.
/// </para>
/// </remarks>
/// <param name="recipe">The recipe as stored, before the save changes it.</param>
internal sealed class OwnIds(Recipe recipe)
{
    private readonly HashSet<Guid> owned =
    [
        .. recipe.Groups.Select(group => group.Id),
        .. recipe.Groups.SelectMany(group => group.Ingredients).Select(line => line.Id),
        .. recipe.Steps.Select(step => step.Id)
    ];

    private readonly Dictionary<Guid, Guid> replaced = [];

    /// <summary>The groups, with every id this recipe does not own replaced.</summary>
    /// <param name="groups">The groups as sent.</param>
    internal IReadOnlyList<IngredientGroupContract> Of(IReadOnlyList<IngredientGroupContract> groups) =>
    [
        .. groups.Select(group => group with
        {
            GroupId = Own(group.GroupId),
            Ingredients = [.. group.Ingredients.Select(line => line with { IngredientId = Own(line.IngredientId) })]
        })
    ];

    /// <summary>
    /// The steps, with every id this recipe does not own replaced — their own,
    /// and the ones they point at.
    /// </summary>
    /// <param name="steps">The steps as sent.</param>
    internal IReadOnlyList<StepContract> Of(IReadOnlyList<StepContract> steps) =>
    [
        .. steps.Select(step => step with
        {
            StepId = Own(step.StepId),
            Segments =
            [
                .. step.Segments.Select(segment => segment with
                {
                    RecipeIngredientId = Own(segment.RecipeIngredientId)
                })
            ],
            Uses = step.Uses?.Select(Own).ToList()
        })
    ];

    private Guid? Own(Guid? id) => id is { } one ? Own(one) : null;

    /// <summary>The id itself when this recipe owns it, otherwise its replacement.</summary>
    /// <remarks>
    /// One replacement per id, whichever list it is met in first, which is what
    /// keeps a step pointing at the line it pointed at.
    /// </remarks>
    private Guid Own(Guid id)
    {
        if (owned.Contains(id))
        {
            return id;
        }

        if (!replaced.TryGetValue(id, out var fresh))
        {
            fresh = CulinaId.New();
            replaced[id] = fresh;
        }

        return fresh;
    }
}
