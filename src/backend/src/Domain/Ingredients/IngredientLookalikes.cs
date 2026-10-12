using Domain.Nutrition;
using Domain.Search;
using Domain.Shopping;

namespace Domain.Ingredients;

/// <summary>An ingredient name a household writes, and in how many of its recipes.</summary>
public sealed record IngredientUse(string Name, int RecipeCount);

/// <summary>Why two names look like one ingredient.</summary>
public enum LookalikeReason
{
    /// <summary>They fold to the same name: "Olivenöl" and "Olivenoel".</summary>
    Spelling,

    /// <summary>They name the same food: "Hühnchenbrustfilet" and "Hähnchenbrust".</summary>
    SameFood,

    /// <summary>One letter or two apart: "Oregano" and "Origano".</summary>
    Typo
}

/// <summary>A proposal to merge one name into another the household uses more.</summary>
public sealed record MergeSuggestion(IngredientUse From, IngredientUse To, LookalikeReason Reason);

/// <summary>Finds the names in a household's recipes that are probably one ingredient.</summary>
/// <remarks>
/// Only proposals: merging rewrites recipes, so a person decides. Each name is proposed to go into
/// the most used name it looks like, following on until a name nothing outranks, so accepting every
/// proposal leaves one name per ingredient and no rule points at a name that is merged away.
/// </remarks>
public static class IngredientLookalikes
{
    /// <summary>A name shorter than this is only a typo of nothing: "Reis" and "Mais" are two foods.</summary>
    private const int ShortestTypo = 5;

    /// <summary>From this length, two slips are still a typo.</summary>
    private const int LongTypo = 9;

    /// <summary>What a word grows by in the plural or when declined: "Tomaten", "grüner", "onions".</summary>
    private static readonly string[] Endings = ["e", "en", "er", "es", "n", "r", "s"];

    /// <summary>Proposes merges, most used names first.</summary>
    /// <param name="uses">Every distinct name the household's recipes use.</param>
    /// <param name="keptApart">Folded names the household said are their own ingredient.</param>
    public static IReadOnlyList<MergeSuggestion> Find(
        IReadOnlyList<IngredientUse> uses,
        IReadOnlySet<string> keptApart)
    {
        ArgumentNullException.ThrowIfNull(uses);
        ArgumentNullException.ThrowIfNull(keptApart);

        var names = uses.OrderBy(use => use, Rank).Select(use => new Name(use)).ToList();
        var into = new Dictionary<Name, (Name To, LookalikeReason Reason)>();

        // Ranked best first, so a name can only go into one before it, which never goes anywhere later.
        for (var later = 1; later < names.Count; later++)
        {
            if (keptApart.Contains(names[later].Key))
            {
                continue;
            }

            for (var earlier = 0; earlier < later; earlier++)
            {
                if (Reason(names[later], names[earlier]) is { } reason)
                {
                    into[names[later]] = (names[earlier], reason);
                    break;
                }
            }
        }

        return [.. names
            .Where(into.ContainsKey)
            .Select(name => new MergeSuggestion(name.Use, Kept(name, into).Use, into[name].Reason))];
    }

    /// <summary>
    /// The name the household already writes for what <paramref name="written"/> probably means,
    /// or null when it writes <paramref name="written"/> itself or nothing like it.
    /// </summary>
    /// <param name="written">A name being typed.</param>
    /// <param name="uses">Every distinct name the household's recipes use.</param>
    public static IngredientUse? Existing(string written, IReadOnlyList<IngredientUse> uses)
    {
        ArgumentNullException.ThrowIfNull(written);
        ArgumentNullException.ThrowIfNull(uses);

        var typed = new Name(new IngredientUse(written.Trim(), 0));

        if (typed.Use.Name.Length == 0 || uses.Any(use => use.Name == typed.Use.Name))
        {
            return null;
        }

        return uses
            .OrderBy(use => use, Rank)
            .FirstOrDefault(use => Reason(typed, new Name(use)) is not null);
    }

    /// <summary>Most used first; then the shorter name, which is usually the plain one.</summary>
    private static readonly Comparer<IngredientUse> Rank = Comparer<IngredientUse>.Create((one, other) =>
    {
        var byCount = other.RecipeCount.CompareTo(one.RecipeCount);
        var byLength = one.Name.Length.CompareTo(other.Name.Length);

        return byCount != 0 ? byCount
            : byLength != 0 ? byLength
            : string.CompareOrdinal(one.Name, other.Name);
    });

    private static Name Kept(Name name, Dictionary<Name, (Name To, LookalikeReason Reason)> into)
    {
        var kept = into[name].To;

        while (into.TryGetValue(kept, out var next))
        {
            kept = next.To;
        }

        return kept;
    }

    private static LookalikeReason? Reason(Name one, Name other)
    {
        if (one.Key == other.Key)
        {
            return LookalikeReason.Spelling;
        }

        // A kind is never a misspelling of its thing: "grüner Spargel" is not "Spargel", "Kokosmilch light" not "Kokosmilch".
        if (!one.Reading.HasSameQualifiers(other.Reading))
        {
            return null;
        }

        // The same thing in another order or case ("Spargel, grün", "grüner Spargel", "Grünspargel"), unless a word
        // about it differs: "große Zwiebel" and "gehackte Zwiebel" are written that way on purpose.
        if (one.Reading.Head.SequenceEqual(other.Reading.Head))
        {
            return one.Reading.Descriptors.Order(StringComparer.Ordinal).SequenceEqual(other.Reading.Descriptors.Order(StringComparer.Ordinal)) &&
                   one.Reading.Preparation.SequenceEqual(other.Reading.Preparation)
                ? LookalikeReason.Spelling
                : null;
        }

        if (InAnotherForm(one, other) || one.Reading.IsSameIngredient(other.Reading))
        {
            return null;
        }

        if (one.Food is not null && other.Food is not null)
        {
            // Two different foods are never a typo of each other, however close they are spelt. One food is not
            // always one thing to buy either: nutrition counts green and white asparagus alike, a shop does not.
            return one.Food == other.Food && !AreKinds(one, other) ? LookalikeReason.SameFood : null;
        }

        return IsTypo(one.Key, other.Key) ? LookalikeReason.Typo : null;
    }

    /// <summary>
    /// Whether the names are one word for word, a word only longer by an ending somewhere: one ingredient in
    /// another number or case, which merging would leave as "1 Tomaten" or "4 Tomate" in a recipe.
    /// </summary>
    private static bool InAnotherForm(Name one, Name other) =>
        one.Words.Length == other.Words.Length &&
        !one.Words.SequenceEqual(other.Words) &&
        one.Words.Zip(other.Words).All(pair => Alike(pair.First, pair.Second));

    /// <summary>
    /// Whether the names are two kinds of one thing rather than two ways of writing it: they share a word but not
    /// all of them ("grüner Spargel" and "Spargel", "weißer Spargel" and "Spargel grün"), or one ends in the
    /// other, as a German compound ends in the thing it is a kind of ("Vollmilch" and "Milch").
    /// </summary>
    private static bool AreKinds(Name one, Name other)
    {
        if (SameWords(one.Words, other.Words) && SameWords(other.Words, one.Words))
        {
            return false;
        }

        return one.Words.Any(word => other.Words.Any(otherWord => Alike(word, otherWord) ||
            word.EndsWith(otherWord, StringComparison.Ordinal) || otherWord.EndsWith(word, StringComparison.Ordinal)));
    }

    private static bool SameWords(string[] words, string[] others) =>
        words.All(word => others.Any(other => Alike(word, other)));

    private static bool Alike(string one, string other) =>
        one == other || EndsDifferently(one, other) || EndsDifferently(other, one);

    private static bool EndsDifferently(string longer, string shorter) =>
        longer.StartsWith(shorter, StringComparison.Ordinal) && Endings.Contains(longer[shorter.Length..]);

    private static bool IsTypo(string one, string other)
    {
        var shorter = Math.Min(one.Length, other.Length);

        if (shorter < ShortestTypo || one[0] != other[0] || !SameDigits(one, other))
        {
            return false;
        }

        var allowed = shorter >= LongTypo ? 2 : 1;

        return Math.Abs(one.Length - other.Length) <= allowed && Distance(one, other) <= allowed;
    }

    // "Mehl 405" and "Mehl 550" are two flours.
    private static bool SameDigits(string one, string other) =>
        one.Where(char.IsAsciiDigit).SequenceEqual(other.Where(char.IsAsciiDigit));

    /// <summary>Edits from one to the other, a swap of neighbours counting as one.</summary>
    private static int Distance(string one, string other)
    {
        var rows = new int[one.Length + 1][];

        for (var i = 0; i <= one.Length; i++)
        {
            rows[i] = new int[other.Length + 1];
            rows[i][0] = i;
        }

        for (var j = 0; j <= other.Length; j++)
        {
            rows[0][j] = j;
        }

        for (var i = 1; i <= one.Length; i++)
        {
            for (var j = 1; j <= other.Length; j++)
            {
                var cost = one[i - 1] == other[j - 1] ? 0 : 1;
                var best = Math.Min(Math.Min(rows[i - 1][j] + 1, rows[i][j - 1] + 1), rows[i - 1][j - 1] + cost);

                if (i > 1 && j > 1 && one[i - 1] == other[j - 2] && one[i - 2] == other[j - 1])
                {
                    best = Math.Min(best, rows[i - 2][j - 2] + 1);
                }

                rows[i][j] = best;
            }
        }

        return rows[one.Length][other.Length];
    }

    private sealed class Name(IngredientUse use)
    {
        public IngredientUse Use { get; } = use;

        public string Key { get; } = ItemName.Fold(use.Name);

        public string? Food { get; } = FoodNames.Match(use.Name)?.Code;

        public string[] Words { get; } = SearchText.FoldAe(use.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public IngredientReading Reading { get; } = IngredientWords.Read(use.Name);
    }
}
