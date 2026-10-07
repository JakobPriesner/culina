using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Update;

/// <summary>The ids a save may keep for its groups, lines and steps: the ones this recipe already has.</summary>
/// <remarks>
/// An id this recipe does not own is treated as a new row and gets a fresh id (steps pointing at it follow).
/// Inserting it as sent failed on the primary key and so revealed that the id exists elsewhere.
/// </remarks>
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
    internal IReadOnlyList<IngredientGroupContract> Of(IReadOnlyList<IngredientGroupContract> groups) =>
    [
        .. groups.Select(group => group with
        {
            GroupId = Own(group.GroupId),
            Ingredients = [.. group.Ingredients.Select(line => line with { IngredientId = Own(line.IngredientId) })]
        })
    ];

    /// <summary>The steps, with every id this recipe does not own replaced, including the ones they point at.</summary>
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

    /// <summary>The id itself when this recipe owns it, otherwise its replacement (one per id, so steps keep pointing at their line).</summary>
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
