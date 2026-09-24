using System.Globalization;
using Domain.Search;

namespace Application.Search;

/// <summary>
/// Reads what a search query asks for beyond its words.
/// </summary>
/// <remarks>
/// <para>
/// "vegetarisches Abendessen unter 30 Minuten mit Kartoffeln" is five
/// requests in one sentence: a diet, a meal, a time, an ingredient, and no
/// words at all to look for. This takes each out in turn — time first, because
/// a number is unambiguous; then diets, meals, cuisines and "schnell"; then
/// what is to be left out; then the sentence around what is to be used — and
/// hands whatever is left to the lexical lanes.
/// </para>
/// <para>
/// Two rules carry most of the risk. <b>Under-parsing beats
/// over-parsing:</b> every sentence rule is anchored to the whole of what is
/// left, so "Nudeln mit Tomatensoße" — where "mit" joins two foods in a dish's
/// name — produces nothing and is searched as typed. And <b>every inference is
/// shown</b>: each comes back with the characters it was read from, so the
/// client can draw it as a chip and remove it by deleting exactly those
/// characters. A parser that does not show its work is one people stop
/// trusting the first time it guesses wrong.
/// </para>
/// <para>
/// Deterministic, allocation-light and without I/O, so it is a static function
/// rather than a service: the same query always reads the same way.
/// </para>
/// </remarks>
public static class QueryUnderstanding
{
    /// <summary>Reads one query.</summary>
    public static QueryIntent Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryIntent(string.Empty, [], []);
        }

        var reading = new Reading(query);

        reading.Times();
        reading.Constraints();
        reading.Exclusions();
        reading.Sentences();

        return reading.Result();
    }

    private static readonly HashSet<string> MinuteUnits =
        new(["min", "mins", "minute", "minuten", "minutes", "minutn"], StringComparer.Ordinal);

    private static readonly HashSet<string> HourUnits =
        new(["std", "stunde", "stunden", "h", "hour", "hours", "hr", "hrs"], StringComparer.Ordinal);

    private static readonly string[][] TimePrefixes =
    [
        ["nicht", "mehr", "als"], ["no", "more", "than"],
        ["weniger", "als"], ["bis", "zu"], ["less", "than"], ["at", "most"], ["in", "under"],
        ["unter"], ["max"], ["maximal"], ["hoechstens"], ["bis"], ["innerhalb"], ["binnen"], ["in"],
        ["under"], ["within"], ["below"]
    ];

    private static readonly HashSet<string> Articles =
        new(["ein", "eine", "einer", "einem", "an", "a"], StringComparer.Ordinal);

    private static readonly HashSet<string> Negations =
        new(["ohne", "kein", "keine", "keinen", "keiner", "without"], StringComparer.Ordinal);

    private static readonly HashSet<string> Connectors =
        new(["und", "and", "oder", "or", "sowie"], StringComparer.Ordinal);

    /// <summary>
    /// Words that carry nothing once something else has been understood:
    /// "Gericht ohne Fleisch" is the vegetarian filter, not a search for the
    /// word "Gericht".
    /// </summary>
    private static readonly HashSet<string> Fillers = new(
        [
            "gericht", "gerichte", "rezept", "rezepte", "rezeptideen", "essen", "mahlzeit", "mahlzeiten",
            "speise", "speisen", "idee", "ideen", "etwas", "was", "irgendwas", "mit", "und", "oder",
            "fuer", "zum", "zur", "ein", "eine", "einen", "einer", "dish", "dishes", "meal", "meals",
            "recipe", "recipes", "food", "idea", "ideas", "something", "anything", "with", "and", "or",
            "for", "a", "an", "the"
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// The sentences people wrap an ingredient in, anchored to the whole of
    /// what is left: <c>*</c> is the ingredient, and a word ending in
    /// <c>?</c> may be missing.
    /// </summary>
    private static readonly string[][] SentenceShapes =
    [
        ["was", "kann", "ich", "heute?|noch?|damit?", "mit", "*", "machen?|kochen?|zubereiten?|backen?"],
        ["was", "koche|mache|backe", "ich", "heute?", "mit", "*"],
        ["etwas|was|irgendwas|irgendetwas", "mit", "*"],
        ["ein?|das?", "rezept|rezepte", "fuer|mit", "*"],
        ["rezept|rezepte|rezeptideen|ideen", "mit|fuer", "*"],
        ["ich", "suche|will|moechte|brauche", "ein?|eine?|einen?|etwas?", "*"],
        ["what", "can", "i", "make|cook|bake", "with", "*"],
        ["what", "to", "make|cook|bake", "with", "*"],
        ["something|anything", "with", "*"],
        ["a?", "recipe|recipes", "for|with", "*"],
        ["i", "want|need", "a?|an?|some?", "*"],
        ["mit|with", "*"]
    ];

    /// <summary>One run of letters and digits in the query, where it was.</summary>
    /// <param name="Text">As typed.</param>
    /// <param name="Folded">Folded the ä → ae way, for comparison.</param>
    /// <param name="Start">Where it begins in the query.</param>
    /// <param name="End">Where it ends, exclusive.</param>
    /// <param name="Negated">Written with a minus directly in front: "-Reis".</param>
    private sealed record Token(string Text, string Folded, int Start, int End, bool Negated);

    private sealed class Reading
    {
        private readonly string query;
        private readonly Token[] tokens;
        private readonly bool[] consumed;
        private readonly List<Inference> applied = [];
        private readonly List<string> ingredients = [];

        internal Reading(string query)
        {
            this.query = query;
            tokens = Tokenize(query);
            consumed = new bool[tokens.Length];
        }

        /// <summary>"unter 30 Minuten", "30 min", "in einer halben Stunde".</summary>
        internal void Times()
        {
            for (var at = 0; at < tokens.Length; at++)
            {
                if (consumed[at] || !TryMinutes(at, out var minutes, out var last))
                {
                    continue;
                }

                var first = at - PrefixBefore(at);

                Take(first, last, InferenceKind.Time, minutes.ToString(CultureInfo.InvariantCulture));
                at = last;
            }
        }

        /// <summary>Diets, meals, cuisines and "schnell", longest name first.</summary>
        internal void Constraints()
        {
            for (var size = 3; size >= 1; size--)
            {
                for (var at = 0; at + size <= tokens.Length; at++)
                {
                    if (!Free(at, size) || tokens[at].Negated
                        || (at > 0 && !consumed[at - 1] && Negations.Contains(tokens[at - 1].Folded)))
                    {
                        continue;
                    }

                    var kind = CulinaryLexicon.Name(Span(at, at + size - 1)) switch
                    {
                        { Kind: ConceptKind.Diet } => InferenceKind.Diet,
                        { Kind: ConceptKind.Meal } => InferenceKind.Meal,
                        { Kind: ConceptKind.Cuisine } => InferenceKind.Cuisine,
                        { Key: "quick" } => InferenceKind.Quick,
                        _ => (InferenceKind?)null
                    };

                    if (kind is { } found)
                    {
                        Take(at, at + size - 1, found, CulinaryLexicon.Name(Span(at, at + size - 1))!.Key);
                    }
                }
            }
        }

        /// <summary>"ohne Zwiebeln", "-Reis", "without nuts", and "und …" after one.</summary>
        internal void Exclusions()
        {
            for (var at = 0; at < tokens.Length; at++)
            {
                if (consumed[at])
                {
                    continue;
                }

                var negationWord = Negations.Contains(tokens[at].Folded) && at + 1 < tokens.Length;

                if (!tokens[at].Negated && !negationWord)
                {
                    continue;
                }

                var from = at;
                var objectAt = negationWord ? at + 1 : at;

                while (objectAt < tokens.Length && !consumed[objectAt])
                {
                    var size = ObjectSize(objectAt);
                    var concept = CulinaryLexicon.Name(Span(objectAt, objectAt + size - 1));
                    var start = tokens[from].Negated && from == objectAt ? tokens[from].Start - 1 : tokens[from].Start;

                    Take(from, objectAt + size - 1, InferenceKind.Exclusion,
                        concept?.Key ?? tokens[objectAt].Text, start);

                    // "ohne Zwiebeln und Knoblauch" leaves out both.
                    var next = objectAt + size;

                    if (next + 1 < tokens.Length && !consumed[next] && !consumed[next + 1]
                        && Connectors.Contains(tokens[next].Folded))
                    {
                        from = next;
                        objectAt = next + 1;
                        at = next;
                    }
                    else
                    {
                        at = next - 1;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The sentence around an ingredient, when the whole of what is left is
        /// one: "was kann ich mit Kartoffeln machen?", "etwas mit Hähnchen".
        /// </summary>
        internal void Sentences()
        {
            var left = Enumerable.Range(0, tokens.Length).Where(at => !consumed[at]).ToArray();
            var rest = left;
            var bare = true;

            // A sentence can sit inside another — "ich suche ein Rezept für
            // Lasagne" — so what one leaves is asked again, a few times at most.
            for (var round = 0; round < 3; round++)
            {
                var inner = rest;
                var shape = SentenceShapes.FirstOrDefault(one => Match(one, 0, inner, 0, out var found) && found.Length > 0);

                if (shape is null)
                {
                    break;
                }

                Match(shape, 0, inner, 0, out rest);
                bare &= shape is ["mit|with", "*"];
            }

            if (rest.Length == left.Length)
            {
                return;
            }

            var found = ReadIngredients(rest);

            // A bare "mit …" left over once everything else was understood is
            // a sentence only if what follows is an ingredient; otherwise the
            // words stay exactly as they were typed.
            if (found.Count == 0 && bare)
            {
                return;
            }

            foreach (var at in left.Except(rest))
            {
                consumed[at] = true;
            }

            var whole = found.Count == 1 && rest.All(at => consumed[at] || Connectors.Contains(tokens[at].Folded));

            if (!whole)
            {
                return;
            }

            // One ingredient and nothing else: the chip is the sentence, so
            // removing it leaves nothing behind.
            var index = applied.IndexOf(found[0]);
            applied[index] = found[0] with
            {
                Text = Span(left[0], left[^1]),
                Start = tokens[left[0]].Start,
                End = tokens[left[^1]].End
            };

            foreach (var at in rest)
            {
                consumed[at] = true;
            }
        }

        internal QueryIntent Result()
        {
            if (!consumed.Contains(true))
            {
                return new QueryIntent(query.Trim(), [], []);
            }

            var free = Enumerable.Range(0, tokens.Length)
                .Where(at => !consumed[at] && (applied.Count == 0 || !Fillers.Contains(tokens[at].Folded)))
                .Select(at => tokens[at].Text);

            return new QueryIntent(
                string.Join(' ', free),
                [.. applied.OrderBy(one => one.Start)],
                [.. ingredients.Distinct(StringComparer.Ordinal)]);
        }

        private List<Inference> ReadIngredients(int[] rest)
        {
            var found = new List<Inference>();

            for (var index = 0; index < rest.Length; index++)
            {
                var at = rest[index];

                if (consumed[at])
                {
                    continue;
                }

                for (var size = Math.Min(3, rest.Length - index); size >= 1; size--)
                {
                    if (!Contiguous(rest, index, size))
                    {
                        continue;
                    }

                    var concept = CulinaryLexicon.Name(Span(at, at + size - 1));

                    if (concept is not { Kind: ConceptKind.Ingredient })
                    {
                        continue;
                    }

                    ingredients.Add(LineName(concept, Span(at, at + size - 1)));
                    found.Add(Take(at, at + size - 1, InferenceKind.Ingredient, concept.Key));
                    index += size - 1;

                    break;
                }
            }

            // The "und" between two ingredients belongs to neither.
            foreach (var at in rest.Where(at => Connectors.Contains(tokens[at].Folded) && found.Count > 1))
            {
                consumed[at] = true;
            }

            return found;
        }

        /// <summary>
        /// Matches a sentence against the tokens left, anchored at both ends.
        /// </summary>
        private bool Match(string[] sentence, int slot, int[] left, int position, out int[] rest)
        {
            rest = [];

            if (slot == sentence.Length)
            {
                return position == left.Length;
            }

            var pattern = sentence[slot];

            if (pattern == "*")
            {
                // The ingredient runs only as far as it must for the rest of
                // the sentence to match: "machen" in "…mit Kartoffeln machen"
                // belongs to the sentence, not to the potatoes.
                for (var end = position + 1; end <= left.Length; end++)
                {
                    if (Match(sentence, slot + 1, left, end, out _))
                    {
                        rest = left[position..end];

                        return true;
                    }
                }

                return false;
            }

            var alternatives = pattern.Split('|');
            var optional = alternatives.All(one => one.EndsWith('?'));
            var words = alternatives.Select(one => one.TrimEnd('?'));

            if (position < left.Length && words.Contains(tokens[left[position]].Folded, StringComparer.Ordinal)
                && Match(sentence, slot + 1, left, position + 1, out rest))
            {
                return true;
            }

            return optional && Match(sentence, slot + 1, left, position, out rest);
        }

        private bool TryMinutes(int at, out int minutes, out int last)
        {
            minutes = 0;
            last = at;
            var word = tokens[at].Folded;

            // "halbe Stunde", "half an hour".
            if (word is "halbe" or "halben" && Unit(at + 1, HourUnits))
            {
                (minutes, last) = (30, at + 1);

                return true;
            }

            if (word == "half" && at + 2 < tokens.Length && tokens[at + 1].Folded == "an" && Unit(at + 2, HourUnits))
            {
                (minutes, last) = (30, at + 2);

                return true;
            }

            int? amount = word switch
            {
                "ein" or "eine" or "einer" or "einem" or "an" or "one" => 1,
                "zwei" or "two" => 2,
                _ => int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null
            };

            if (amount is { } whole && Unit(at + 1, MinuteUnits) && word.All(char.IsAsciiDigit))
            {
                (minutes, last) = (whole, at + 1);
            }
            else if (amount is { } hours && Unit(at + 1, HourUnits))
            {
                (minutes, last) = (hours * 60, at + 1);
            }
            else
            {
                // "30min", written as one word.
                var digits = new string([.. word.TakeWhile(char.IsAsciiDigit)]);
                var unit = word[digits.Length..];

                if (digits.Length == 0 || !int.TryParse(digits, CultureInfo.InvariantCulture, out var joined))
                {
                    return false;
                }

                minutes = MinuteUnits.Contains(unit) ? joined : HourUnits.Contains(unit) ? joined * 60 : 0;
            }

            return minutes is > 0 and <= 24 * 60;
        }

        private bool Unit(int at, HashSet<string> units) =>
            at < tokens.Length && !consumed[at] && units.Contains(tokens[at].Folded);

        /// <summary>
        /// How many tokens before a time belong to it: "unter", "weniger als",
        /// and an article in between — "in einer halben Stunde".
        /// </summary>
        private int PrefixBefore(int at)
        {
            var article = at > 0 && !consumed[at - 1] && Articles.Contains(tokens[at - 1].Folded) ? 1 : 0;

            return article + Prefix(at - article);
        }

        private int Prefix(int at)
        {
            foreach (var prefix in TimePrefixes)
            {
                var from = at - prefix.Length;

                if (from >= 0 && Free(from, prefix.Length)
                    && prefix.Select((word, offset) => tokens[from + offset].Folded == word).All(same => same))
                {
                    return prefix.Length;
                }
            }

            return 0;
        }

        /// <summary>How many tokens the thing being left out takes: its name, or one word.</summary>
        private int ObjectSize(int at)
        {
            for (var size = 3; size > 1; size--)
            {
                if (Free(at, size) && CulinaryLexicon.Name(Span(at, at + size - 1)) is not null)
                {
                    return size;
                }
            }

            return 1;
        }

        private Inference Take(int first, int last, InferenceKind kind, string value, int? start = null)
        {
            for (var at = first; at <= last; at++)
            {
                consumed[at] = true;
            }

            var from = start ?? tokens[first].Start;
            var inference = new Inference(kind, value, query[from..tokens[last].End], from, tokens[last].End);

            applied.Add(inference);

            return inference;
        }

        private bool Free(int at, int size)
        {
            for (var offset = 0; offset < size; offset++)
            {
                if (at + offset >= tokens.Length || consumed[at + offset])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contiguous(int[] rest, int index, int size) =>
            index + size <= rest.Length && rest[index + size - 1] - rest[index] == size - 1;

        private string Span(int first, int last) => query[tokens[first].Start..tokens[last].End];
    }

    /// <summary>
    /// The name an ingredient line would use for what was typed.
    /// </summary>
    /// <remarks>
    /// The ranking counts ingredients by name, and a line says "Kartoffel" where
    /// a query says "Kartoffeln". So it is the shortest of the concept's own
    /// forms that the typed word begins with — four letters at least, or "Ei"
    /// would be found in "Reis".
    /// </remarks>
    private static string LineName(Concept concept, string typed)
    {
        var folded = SearchText.FoldAe(typed);

        return concept.De.Concat(concept.En)
            .Where(form => form.Length >= 4 && folded.StartsWith(SearchText.FoldAe(form), StringComparison.Ordinal))
            .OrderBy(form => form.Length)
            .FirstOrDefault() ?? typed;
    }

    private static Token[] Tokenize(string query)
    {
        var tokens = new List<Token>();

        for (var at = 0; at < query.Length;)
        {
            if (!char.IsLetterOrDigit(query[at]))
            {
                at++;

                continue;
            }

            var start = at;

            while (at < query.Length && char.IsLetterOrDigit(query[at]))
            {
                at++;
            }

            var text = query[start..at];
            var negated = start > 0 && query[start - 1] == '-' && (start == 1 || char.IsWhiteSpace(query[start - 2]));

            tokens.Add(new Token(text, SearchText.FoldAe(text), start, at, negated));
        }

        return [.. tokens];
    }
}
