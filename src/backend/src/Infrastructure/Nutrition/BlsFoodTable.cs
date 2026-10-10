using System.Globalization;
using System.Reflection;
using Application.Abstractions;
using Domain.Nutrition;
using Domain.Search;

namespace Infrastructure.Nutrition;

/// <summary>
/// The Bundeslebensmittelschlüssel, read once from the <c>bls.tsv</c> compiled into this assembly.
/// </summary>
/// <remarks>
/// A malformed file is a release defect, so it throws when loaded (at startup) instead of serving a
/// wrong number. <c>-</c> is unknown (null), never zero; <c>tr</c>, a trace, is zero.
/// </remarks>
internal sealed class BlsFoodTable : IFoodTable
{
    private const string ResourceName = "Infrastructure.Nutrition.bls.tsv";
    private const int Columns = 11;

    private readonly Dictionary<string, Food> byCode;
    private readonly List<Searchable> searchable;

    private BlsFoodTable(List<Food> foods)
    {
        byCode = foods.ToDictionary(food => food.Code, StringComparer.Ordinal);
        searchable = [.. foods.Select(food =>
            new Searchable(food, SearchText.FoldAe(food.NameDe), SearchText.FoldAe(food.NameEn)))];
    }

    /// <inheritdoc />
    public int Count => byCode.Count;

    /// <summary>Every food, for tests that check the whole table.</summary>
    internal IEnumerable<Food> All => byCode.Values;

    /// <summary>Reads the embedded table.</summary>
    internal static BlsFoodTable Load()
    {
        using var stream = typeof(BlsFoodTable).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return Parse(reader);
    }

    /// <summary>Reads a table in the shape of <c>bls.tsv</c>, or throws saying which line is wrong.</summary>
    /// <param name="reader">The text, header first.</param>
    internal static BlsFoodTable Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _ = reader.ReadLine();

        var foods = new List<Food>();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var number = 2; reader.ReadLine() is { } line; number++)
        {
            var food = ParseLine(line, number);

            if (!codes.Add(food.Code))
            {
                throw Defect(number, $"code {food.Code} appears twice");
            }

            foods.Add(food);
        }

        return new BlsFoodTable(foods);
    }

    /// <inheritdoc />
    public Food? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return byCode.GetValueOrDefault(code);
    }

    /// <inheritdoc />
    public IReadOnlyList<Food> Search(string query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);

        var typed = SearchText.FoldAe(query);

        if (typed.Length == 0 || limit <= 0)
        {
            return [];
        }

        return [.. searchable
            .Select(one => (one.Food, Rank: one.Rank(typed)))
            .Where(one => one.Rank.Tier is not null)
            .OrderBy(one => one.Rank.Tier)
            .ThenBy(one => one.Rank.Length)
            .ThenBy(one => one.Food.Code, StringComparer.Ordinal)
            .Take(limit)
            .Select(one => one.Food)];
    }

    private static Food ParseLine(string line, int number)
    {
        var cells = line.Split('\t');

        if (cells.Length != Columns)
        {
            throw Defect(number, $"{cells.Length} columns instead of {Columns}");
        }

        var values = cells[3..].Select(cell => ParseValue(cell, number)).ToArray();

        return new Food(
            cells[0],
            cells[1],
            cells[2],
            new Nutrients(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]));
    }

    private static decimal? ParseValue(string cell, int number)
    {
        if (cell == "-")
        {
            return null;
        }

        if (cell == "tr")
        {
            return 0m;
        }

        if (!decimal.TryParse(cell, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            throw Defect(number, $"'{cell}' is not a number");
        }

        return value < 0 ? throw Defect(number, $"{cell} is negative") : value;
    }

    private static InvalidOperationException Defect(int line, string what) =>
        new($"{ResourceName}, line {line}: {what}.");

    private sealed record Searchable(Food Food, string De, string En)
    {
        /// <summary>How well the better of the two names fits: 0 starts with it, 1 a word starts with it, 2 contains it.</summary>
        internal (int? Tier, int Length) Rank(string typed)
        {
            var de = RankOf(De, typed);
            var en = RankOf(En, typed);

            return de.Tier is null ? en : en.Tier is null ? de : de.CompareTo(en) <= 0 ? de : en;
        }

        private static (int? Tier, int Length) RankOf(string name, string typed)
        {
            if (name.StartsWith(typed, StringComparison.Ordinal))
            {
                return (0, name.Length);
            }

            if (name.Contains(' ' + typed, StringComparison.Ordinal))
            {
                return (1, name.Length);
            }

            return name.Contains(typed, StringComparison.Ordinal) ? (2, name.Length) : (null, 0);
        }
    }
}
