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
        IReadOnlyList<Guid> library,
        IReadOnlyList<string> words,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(words);

        var rows = await executor.QueryAsync<Spelling>(
            """
            with vocabulary as (
                select distinct word from (
                    select unnest(string_to_array(d.title_ae, ' ')) as word
                    from recipe_search_documents d
                    where d.household_id = any(@library)
                    union all
                    select unnest(string_to_array(culina_fold_ae(i.name), ' '))
                    from recipe_ingredients i
                    join ingredient_groups g on g.id = i.group_id
                    join recipes r on r.id = g.recipe_id
                    where r.household_id = any(@library)
                    union all
                    select unnest(string_to_array(culina_fold_ae(t.name), ' '))
                    from tags t
                    where t.household_id = any(@library)
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
            new { library = library.ToArray(), words = words.ToArray(), threshold = Threshold },
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.Typed, row => row.Meant, StringComparer.Ordinal);
    }

    public async Task<Completions> CompletionsAsync(
        IReadOnlyList<Guid> library,
        string typed,
        int perKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var parameters = new { library = library.ToArray(), typed, perKind };

        // A word of the name that begins with what was typed, in either fold:
        // "häh" finds Hähnchen-Curry and Brathähnchen alike, and "haeh" and
        // "hah" find them too.
        var recipes = await executor.QueryAsync<RecipeCompletion>(
            $"""
            with {Typed}
            select r.id as recipe_id, r.title, r.image_id,
                   case when r.prep_minutes is null and r.cook_minutes is null then null
                        else coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0) end as total_minutes
            from recipe_search_documents d
            cross join typed t
            join recipes r on r.id = d.recipe_id
            where d.household_id = any(@library)
              and ({Begins("d.title_ae", "d.title_a")})
            -- A title that starts with the word, then the shortest: the one
            -- most nearly called what was typed.
            order by (d.title_ae like t.ae || '%' or d.title_a like t.a || '%') desc,
                     length(d.title_ae), d.title_ae
            limit @perKind;
            """,
            parameters,
            cancellationToken).ConfigureAwait(false);

        var ingredients = await executor.QueryAsync<IngredientCompletion>(
            $"""
            with {Typed}
            select min(i.name) as name,
                   count(distinct r.id)::int as recipe_count,
                   count(distinct r.id) filter (
                       where (r.prep_minutes is not null or r.cook_minutes is not null)
                         and coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0) <= 30)::int as quick_count
            from recipe_ingredients i
            cross join typed t
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = any(@library)
              and ({Begins("i.name_ae", "i.name_a")})
            group by i.name_ae
            order by recipe_count desc, name
            limit @perKind;
            """,
            parameters,
            cancellationToken).ConfigureAwait(false);

        var tags = await executor.QueryAsync<TagCompletion>(
            $"""
            with {Typed}
            select tg.slug, min(tg.name) as name, count(rt.recipe_id)::int as recipe_count
            from tags tg
            cross join typed t
            left join recipe_tags rt on rt.tag_id = tg.id
            where tg.household_id = any(@library)
              and ({Begins("culina_fold_ae(tg.name)", "culina_fold_a(tg.name)")})
            -- By slug, not by row: an inherited household may carry the same
            -- tag, and it is one word to filter by, not two.
            group by tg.slug
            order by recipe_count desc, name
            limit @perKind;
            """,
            parameters,
            cancellationToken).ConfigureAwait(false);

        return new Completions(recipes, ingredients, tags);
    }

    private const string Typed = "typed as (select culina_fold_ae(@typed) as ae, culina_fold_a(@typed) as a)";

    /// <summary>Whether a word of a folded name, in either fold, begins with what was typed.</summary>
    private static string Begins(string ae, string a) =>
        $"{ae} like t.ae || '%' or {ae} like '% ' || t.ae || '%' "
        + $"or {a} like t.a || '%' or {a} like '% ' || t.a || '%'";

    private sealed record Spelling
    {
        public string Typed { get; init; } = string.Empty;

        public string Meant { get; init; } = string.Empty;
    }
}
