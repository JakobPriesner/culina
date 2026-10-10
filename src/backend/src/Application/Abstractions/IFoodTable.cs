using Domain.Nutrition;

namespace Application.Abstractions;

/// <summary>The foods of the Bundeslebensmittelschlüssel, held in memory.</summary>
public interface IFoodTable
{
    /// <summary>How many foods the table holds.</summary>
    int Count { get; }

    /// <summary>The food with this BLS code, or null.</summary>
    /// <param name="code">The code, such as <c>Q611000</c>.</param>
    Food? Find(string code);

    /// <summary>
    /// Foods whose German or English name fits what was typed, best first: names that start with it,
    /// then names with a word that starts with it, then names that contain it, shorter names first.
    /// </summary>
    /// <param name="query">What was typed; blank finds nothing.</param>
    /// <param name="limit">How many foods at most.</param>
    IReadOnlyList<Food> Search(string query, int limit);
}
