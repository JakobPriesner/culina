using Domain.Ingredients;

namespace Application.Abstractions;

/// <summary>Reads the ingredient names a household writes, and keeps its merges of them.</summary>
public interface IIngredientNameRepository
{
    /// <summary>Every distinct name in the household's own recipes, with how many recipes use it.</summary>
    /// <remarks>Not those it inherits: a merge can only rewrite the household's own recipes.</remarks>
    Task<IReadOnlyList<IngredientUse>> UsesAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Every merge the household has made, by the name that goes away.</summary>
    Task<IReadOnlyList<IngredientMerge>> MergesAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a merge and applies it: the household's recipes are rewritten, rules that pointed at
    /// the merged name point on, and nutrition and shopping choices made for it move along.
    /// </summary>
    /// <returns>How many recipes were rewritten.</returns>
    Task<int> MergeAsync(
        Guid householdId,
        IngredientMerge merge,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Forgets a merge; what it already rewrote stays rewritten.</summary>
    Task RemoveMergeAsync(Guid householdId, string fromKey, CancellationToken cancellationToken);

    /// <summary>The folded names the household keeps apart from every suggestion.</summary>
    Task<IReadOnlySet<string>> KeptApartAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Stops suggesting a name for merging.</summary>
    Task KeepApartAsync(Guid householdId, string nameKey, DateTimeOffset now, CancellationToken cancellationToken);
}
