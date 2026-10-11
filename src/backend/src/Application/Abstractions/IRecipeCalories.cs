using Domain.Nutrition;

namespace Application.Abstractions;

/// <summary>Per-portion energy calculated with the reading household's nutrition choices.</summary>
public interface IRecipeCalories
{
    /// <summary>Reads a batch; null ids reads the visible library. Unknown figures are absent.</summary>
    Task<IReadOnlyDictionary<Guid, LabelValue>> ReadAsync(
        Guid householdId,
        IReadOnlyList<Guid> library,
        IReadOnlyCollection<Guid>? recipeIds,
        CancellationToken cancellationToken);
}
