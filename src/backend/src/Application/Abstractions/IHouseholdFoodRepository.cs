using Domain.Nutrition;

namespace Application.Abstractions;

/// <summary>A food a household typed from a package, and which household did.</summary>
/// <param name="Id">The food's id.</param>
/// <param name="Food">The food; its code is <see cref="HouseholdFoods.CodeOf"/> of the id.</param>
/// <param name="HouseholdId">The household that typed it.</param>
/// <param name="Gtin">The barcode of the package it was typed or taken from, when it was scanned.</param>
/// <param name="Source">Where its values come from (<see cref="HouseholdFoodSources"/>).</param>
public sealed record HouseholdFood(
    Guid Id,
    Food Food,
    Guid HouseholdId,
    string? Gtin = null,
    string Source = HouseholdFoodSources.Package);

/// <summary>Keeps the foods households typed from a package's label.</summary>
public interface IHouseholdFoodRepository
{
    /// <summary>
    /// The foods of a household and of those it inherits from, by name: an heir counts with the foods its parent
    /// chose, so it can choose them too.
    /// </summary>
    Task<IReadOnlyList<HouseholdFood>> ForLibraryAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);

    /// <summary>The nearest household's newest food from a package with this barcode, or null when none was kept.</summary>
    /// <param name="library">The household first, then what it inherits, nearest first.</param>
    /// <param name="gtin">The barcode, normalised.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<HouseholdFood?> FindByGtinAsync(IReadOnlyList<Guid> library, string gtin, CancellationToken cancellationToken);

    /// <summary>Keeps a new food.</summary>
    Task AddAsync(HouseholdFood food, DateTimeOffset now, CancellationToken cancellationToken);

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
