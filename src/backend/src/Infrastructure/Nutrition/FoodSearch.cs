using Domain.Nutrition;
using Domain.Search;

namespace Infrastructure.Nutrition;

/// <summary>
/// Finds foods by the words of their German or English name, in any order, forgiving a missing
/// umlaut and a small typo.
/// </summary>
/// <remarks>
/// <para>
/// Every typed word must fit some word of one name: "fettarme Milch" finds "Milch fettarm, frisch".
/// Two neighbouring name words also count as one (see <see cref="Joined"/>).
/// A typed word fits a name word that starts with it, else one that holds it (three letters or
/// more: "milch" in "Vollmilch"), else one it is a typo of (see <see cref="TypedWord"/>).
/// Names and query are folded both ways (ä → ae and ä → a) and every pair is tried, so
/// "Hahnchen", "Haehnchen" and "Hähnchen" all find "Hähnchen".
/// </para>
/// <para>
/// Best first: by the worst-fitting typed word, then names whose first word (or first two joined)
/// fits the first typed word, then shorter names, then the code. Each distinct name word (about 21,700 with the joined
/// pairs) is held once, so a typed word is compared with each of them once and the foods only look
/// the answer up: one search of the whole table took 0.2 to 3.6 ms in a Release build (a throwaway
/// Stopwatch test, October 2026).
/// </para>
/// </remarks>
internal sealed class FoodSearch
{
    private const byte Start = 0;
    private const byte Inside = 1;
    private const byte Typo = 2;
    private const byte NoFit = byte.MaxValue;
    private const int ShortestJoined = 3;

    private readonly Word[] words;
    private readonly List<Entry> entries = [];

    internal FoodSearch(IEnumerable<Food> foods)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var plain = new HashSet<int>();

        foreach (var food in foods)
        {
            var names = new List<Name>();

            foreach (var name in new[] { food.NameDe, food.NameEn })
            {
                var length = SearchText.FoldAe(name).Length;

                foreach (var form in Forms(name))
                {
                    var own = form.Select(word => IdOf(word, ids)).ToList();
                    plain.UnionWith(own);

                    names.Add(new Name(
                        [.. own, .. Joined(form).Select(word => IdOf(word, ids))],
                        [.. Opening(form).Select(word => IdOf(word, ids))],
                        length));
                }
            }

            entries.Add(new Entry(food, names));
        }

        words = new Word[ids.Count];

        foreach (var (word, id) in ids)
        {
            words[id] = new Word(word, Joined: !plain.Contains(id));
        }
    }

    /// <summary>The foods that fit what was typed, best first.</summary>
    internal IReadOnlyList<Food> Find(string query, int limit)
    {
        var typed = Forms(query);

        if (typed.Count == 0 || limit <= 0)
        {
            return [];
        }

        // For each fold of the query, for each typed word: how it fits each distinct name word.
        var fits = typed.Select(form => form.Select(FitsOf).ToArray()).ToList();
        var found = new List<(Rank Rank, Food Food)>();

        foreach (var entry in entries)
        {
            Rank? best = null;

            foreach (var form in fits)
            {
                foreach (var name in entry.Names)
                {
                    if (RankOf(form, name) is { } rank && (best is null || rank.CompareTo(best.Value) < 0))
                    {
                        best = rank;
                    }
                }
            }

            if (best is { } fitting)
            {
                found.Add((fitting, entry.Food));
            }
        }

        return [.. found
            .OrderBy(one => one.Rank)
            .ThenBy(one => one.Food.Code, StringComparer.Ordinal)
            .Take(limit)
            .Select(one => one.Food)];
    }

    /// <summary>The words of a text, folded ä → ae and, where that differs, ä → a.</summary>
    private static List<string[]> Forms(string text) =>
        [.. new[] { SearchText.FoldAe(text), SearchText.FoldA(text) }
            .Distinct(StringComparer.Ordinal)
            .Select(fold => fold.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(form => form.Length > 0)];

    /// <summary>
    /// Each two neighbouring words of a name written as one, so "Haferflocken" finds "Hafer Flocken"
    /// and "Hähnchenbrustfilet" "Hähnchen Brustfilet". Only words of three letters or more join:
    /// "Eis" is not "Ei, Schale", nor "15" "1,5 %". A typed word fits a joined pair by its start
    /// or a typo, never inside it (see <see cref="TypedWord.Fit"/>).
    /// </summary>
    private static IEnumerable<string> Joined(string[] form) =>
        form.Zip(form.Skip(1))
            .Where(pair => pair.First.Length >= ShortestJoined && pair.Second.Length >= ShortestJoined)
            .Select(pair => pair.First + pair.Second);

    /// <summary>The words a name opens with: its first, and its first two joined if they join.</summary>
    private static IEnumerable<string> Opening(string[] form) => [form[0], .. Joined(form[..Math.Min(2, form.Length)])];

    private static int IdOf(string word, Dictionary<string, int> ids)
    {
        if (!ids.TryGetValue(word, out var id))
        {
            id = ids.Count;
            ids.Add(word, id);
        }

        return id;
    }

    private byte[] FitsOf(string typedWord)
    {
        var typed = new TypedWord(typedWord);
        var fits = new byte[words.Length];

        for (var id = 0; id < words.Length; id++)
        {
            fits[id] = typed.Fit(words[id]);
        }

        return fits;
    }

    /// <summary>How well one name fits the typed words, or null when one of them fits no word of it.</summary>
    /// <param name="fits">Per typed word, its fit to each distinct name word.</param>
    /// <param name="name">The name, as ids of its words.</param>
    private static Rank? RankOf(byte[][] fits, Name name)
    {
        var worst = Start;

        foreach (var typed in fits)
        {
            var best = BestFit(typed, name.Words);

            if (best == NoFit)
            {
                return null;
            }

            worst = Math.Max(worst, best);
        }

        var leads = BestFit(fits[0], name.Opening) == BestFit(fits[0], name.Words);

        return new Rank(worst, leads ? 0 : 1, name.Length);
    }

    private static byte BestFit(byte[] fits, int[] words)
    {
        var best = NoFit;

        foreach (var word in words)
        {
            best = Math.Min(best, fits[word]);
        }

        return best;
    }

    /// <summary>Best first: the worst typed word's fit, then a fitting first word, then shorter.</summary>
    private readonly record struct Rank(byte Fit, int Trailing, int Length) : IComparable<Rank>
    {
        public int CompareTo(Rank other) => (Fit, Trailing, Length).CompareTo((other.Fit, other.Trailing, other.Length));
    }

    /// <summary>A distinct word of the names, and whether it is only ever two of them joined.</summary>
    private sealed record Word(string Text, bool Joined);

    /// <summary>
    /// One way to write one name: the ids of its words (each two neighbours joined among them), of
    /// those it opens with, and its length to rank by.
    /// </summary>
    private sealed record Name(int[] Words, int[] Opening, int Length);

    private sealed record Entry(Food Food, List<Name> Names);

    /// <summary>One typed word, and how well it fits a word of a name.</summary>
    /// <remarks>
    /// A typo is up to one edit (a letter added, dropped, changed, or two neighbours swapped) for a
    /// word of five letters or more, two for nine or more, away from the name word or from a start
    /// of it: "Bananne" finds "Banane", "Joghrut" "Joghurt", "Kartofel" "Kartoffelpüree".
    /// </remarks>
    private sealed class TypedWord
    {
        /// <summary>More edits than any typo allows.</summary>
        private const int TooFar = 1000;

        private readonly string text;
        private readonly int typos;

        /// <summary>
        /// The edit distances, reused for every name word. Only the band within <see cref="typos"/>
        /// of the diagonal is ever filled: a cell further off is more edits away than a typo allows,
        /// so it holds <see cref="TooFar"/> for good. The band took the slowest search measured
        /// ("Kartoffelpüree mit Milch") from 5.1 to about 3 ms (Release, October 2026).
        /// </summary>
        private readonly int[][] edits;

        internal TypedWord(string text)
        {
            this.text = text;
            typos = text.Length >= 9 ? 2 : text.Length >= 5 ? 1 : 0;
            edits = [.. Enumerable.Range(0, text.Length + 1).Select(row =>
                Enumerable.Range(0, text.Length + typos + 1).Select(column =>
                    Math.Abs(row - column) <= typos && Math.Min(row, column) == 0 ? row + column : TooFar).ToArray())];
        }

        /// <summary>
        /// How well it fits. Two words joined fit only where the typed word starts with the first
        /// of them or is a typo of the whole pair: inside or a typo of a start of it, "tee" would
        /// find "Blutwürste erwärmt" and "fettarm" "Fett, aromatisiert".
        /// </summary>
        internal byte Fit(Word word)
        {
            if (word.Text.StartsWith(text, StringComparison.Ordinal))
            {
                return Start;
            }

            if (!word.Joined && text.Length >= 3 && word.Text.Contains(text, StringComparison.Ordinal))
            {
                return Inside;
            }

            return typos > 0 && EditsAway(word.Text, orAStartOfIt: !word.Joined) <= typos ? Typo : NoFit;
        }

        /// <summary>
        /// The fewest edits from the typed word to the name word, or to it or a start of it
        /// (optimal string alignment): the table's last cell, or the smallest number in its last
        /// row. Stops at the first row that is all too far, as every later row then is.
        /// </summary>
        private int EditsAway(string word, bool orAStartOfIt)
        {
            var columns = Math.Min(word.Length, text.Length + typos);

            if (columns < text.Length - typos)
            {
                return TooFar;
            }

            var closest = 0;

            for (var row = 1; row <= text.Length && closest <= typos; row++)
            {
                closest = edits[row][0];

                for (var column = Math.Max(1, row - typos); column <= Math.Min(columns, row + typos); column++)
                {
                    var changed = text[row - 1] == word[column - 1] ? 0 : 1;
                    var fewest = Math.Min(
                        Math.Min(edits[row - 1][column] + 1, edits[row][column - 1] + 1),
                        edits[row - 1][column - 1] + changed);

                    if (row > 1 && column > 1 && text[row - 1] == word[column - 2] && text[row - 2] == word[column - 1])
                    {
                        fewest = Math.Min(fewest, edits[row - 2][column - 2] + 1);
                    }

                    edits[row][column] = fewest;
                    closest = Math.Min(closest, fewest);
                }
            }

            if (closest > typos || orAStartOfIt)
            {
                return closest;
            }

            return word.Length == columns ? edits[text.Length][columns] : TooFar;
        }
    }
}
