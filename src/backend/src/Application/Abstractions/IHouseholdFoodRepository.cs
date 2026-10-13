using Domain.Nutrition;

namespace Application.Abstractions;

/// <summary>A food a household typed from a package, and which household did.</summary>
/// <param name="Id">The food's id.</param>
/// <param name="Food">The food; its code is <see cref="HouseholdFoods.CodeOf"/> of the id.</param>
/// <param name="HouseholdId">The household that typed it.</param>
public sealed record HouseholdFood(Guid Id, Food Food, Guid HouseholdId);

/// <summary>Keeps the foods households typed from a package's label.</summary>
public interface IHouseholdFoodRepository
{
    /// <summary>
    /// The foods of a household and of those it inherits from, by name: an heir counts with the foods its parent
    /// chose, so it can choose them too.
    /// </summary>
    Task<IReadOnlyList<HouseholdFood>> ForLibraryAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);

    /// <summary>Keeps a new food.</summary>
    Task AddAsync(Guid householdId, Guid id, Food food, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Removes one of the household's foods, and every choice of it, which counts as not chosen again.</summary>
    Task RemoveAsync(Guid householdId, Guid id, CancellationToken cancellationToken);
}

/// <summary>Looks a food up in the table and among a household's own.</summary>
public static class FoodLookup
{
    /// <summary>The food with a code: a household's own, else the table's.</summary>
    /// <param name="foods">The table.</param>
    /// <param name="own">The household foods that may be meant.</param>
    public static Func<string, Food?> With(IFoodTable foods, IEnumerable<HouseholdFood> own)
    {
        ArgumentNullException.ThrowIfNull(foods);

        var byCode = own.ToDictionary(one => one.Food.Code, one => one.Food, StringComparer.Ordinal);

        return code => byCode.GetValueOrDefault(code) ?? foods.Find(code);
    }
}
