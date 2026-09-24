using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Corrects a search against the words one household's recipes use.
/// </summary>
/// <remarks>
/// <para>
/// The dictionary is the household's titles, ingredient names and tags, folded
/// the way the search folds them, and a word is corrected to the nearest of
/// them by trigram similarity. A German word list would offer "Bologneser"
/// for "Bolgnese"; this offers "bolognese", because that is what is actually
/// there — and for a word nothing in the kitchen resembles, it offers nothing,
/// which is right.
/// </para>
/// <para>
/// Only ever asked when a search found nothing, so the cost of building the
/// vocabulary on the fly is paid by the rare query that needs it rather than
/// stored for every recipe.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
internal sealed class SearchVocabulary(DbExecutor executor) : ISearchVocabulary
{
    /// <summary>
    /// How alike two words must be for one to be taken as the other misspelt.
    /// </summary>
    /// <remarks>
    /// Lower than the typo lane's half, because this only runs once nothing
    /// matched at all — and the correction is always shown, with the original
    /// one tap away.
    /// </remarks>
    internal const double Threshold = 0.4d;

    public async Task<IReadOnlyDictionary<string, string>> SpellingsAsync(
        Guid householdId,
        IReadOnlyList<string> words,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(words);

        var rows = await executor.QueryAsync<Spelling>(
            """
            with vocabulary as (
                select distinct word from (
                    select unnest(string_to_array(d.title_ae, ' ')) as word
                    from recipe_search_documents d
                    where d.household_id = @householdId
                    union all
                    select unnest(string_to_array(culina_fold_ae(i.name), ' '))
                    from recipe_ingredients i
                    join ingredient_groups g on g.id = i.group_id
                    join recipes r on r.id = g.recipe_id
                    where r.household_id = @householdId
                    union all
                    select unnest(string_to_array(culina_fold_ae(t.name), ' '))
                    from tags t
                    where t.household_id = @householdId
                ) as every_word
                where length(word) >= 4)
            select distinct on (typed) typed, v.word as meant
            from unnest(@words::text[]) as typed
            cross join vocabulary v
            where similarity(typed, v.word) >= @threshold
              -- A word the kitchen already uses is not a misspelling of
              -- another one, however alike the two look.
              and not exists (select 1 from vocabulary known where known.word = typed)
            order by typed, similarity(typed, v.word) desc, v.word;
            """,
            new { householdId, words = words.ToArray(), threshold = Threshold },
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.Typed, row => row.Meant, StringComparer.Ordinal);
    }

    private sealed record Spelling
    {
        public string Typed { get; init; } = string.Empty;

        public string Meant { get; init; } = string.Empty;
    }
}
