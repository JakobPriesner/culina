-- Recipe search: one derived document per recipe, in the shapes retrieval needs.
--
-- The free-text search this replaces was three `ilike '%q%'` predicates over
-- the title, the description and the ingredient names, ordered by how recently
-- a recipe had been edited. It could not survive a typo, an inflection, a
-- synonym, or an umlaut typed as `ae`, and a text query had no effect on the
-- order at all.
--
-- Everything here is derived and nothing is authored. The document is written
-- in the same transaction as the recipe it describes — the decision
-- step_ingredient_refs already made, for the same reason: an index that is
-- eventually consistent with its source is an index that occasionally cannot
-- find a recipe somebody has just saved, and nothing will have said so.
--
-- Not a materialised view. Those refresh wholesale, and refreshing the whole
-- household's library because one person fixed one typo is not a trade worth
-- making for a table this cheap to maintain a row at a time.

-- ── Text search configurations ───────────────────────────────────────────────
--
-- One per content language, because recipes.language already says which
-- language a recipe is written in. Guessing the language of a two-word query is
-- hopeless ("Pasta", "Curry" and "Butter" are both languages at once); knowing
-- the language of a document is free, and it is where the information is.
--
-- unaccent runs BEFORE the stemmer, and that ordering is the whole fix for
-- German: the `german` Snowball stemmer does not remove umlauts, so without it
-- `Hähnchen` and `Hahnchen` produce different lexemes and never meet.
--
-- What this still cannot do is split compound nouns — Snowball strips suffixes
-- by letter pattern and owns no dictionary, so `Hähnchenbrustfilet` is one
-- lexeme forever. That is what the trigram half of this file is for, and it is
-- why replacing the old `ilike` with full-text search *alone* would have been a
-- regression on the app's primary content language rather than an upgrade.

create text search configuration culina_de (copy = german);

alter text search configuration culina_de
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, german_stem;

create text search configuration culina_en (copy = english);

alter text search configuration culina_en
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, english_stem;

comment on text search configuration culina_de is
    'German, with unaccent ahead of the stemmer so an umlaut and its bare vowel are one lexeme.';

-- ── Folding ─────────────────────────────────────────────────────────────────
--
-- Two transliterations, because German is written in both and neither is
-- wrong. A German keyboard that is not to hand gives `Muesli`; stripping the
-- diacritic — which is what unaccent does, and what every search engine means
-- by "fold" — gives `Musli`. They do not meet, so both are stored and both are
-- asked for. Domain/Shopping/ItemName.Fold already made this call for the
-- shopping list, and made it the `ue` way; this is the same decision, taken
-- twice on purpose.
--
-- The cluster runs with --locale=C (see compose.yaml), where lower() only
-- folds ASCII: lower('Ä') is 'Ä'. Every mapping below therefore handles the
-- capital itself and lower() is applied last, once the text is ASCII.
--
-- Everything that is not a letter or a digit becomes a space. That is a note
-- about spelling and a load-bearing fact about safety: the search builds LIKE
-- patterns by concatenation, so a query containing `%` or `_` would otherwise
-- be a wildcard nobody asked for — `%` matching the whole library. Escaping at
-- each call site is the version of this that gets forgotten once; removing
-- them in the one function every string already passes through is the version
-- that cannot be.

create function culina_fold_ae(value text) returns text
    language sql
    immutable
    strict
    parallel safe
    return btrim(regexp_replace(
        lower(translate(
            replace(replace(replace(replace(replace(replace(replace(replace(replace(
                value,
                'ß', 'ss'), 'ẞ', 'ss'),
                'Ä', 'Ae'), 'ä', 'ae'),
                'Ö', 'Oe'), 'ö', 'oe'),
                'Ü', 'Ue'), 'ü', 'ue'),
                'æ', 'ae'),
            'ÀÁÂÃÅÈÉÊËÌÍÎÏÒÓÔÕØÙÚÛÑÇÝàáâãåèéêëìíîïòóôõøùúûñçýÿ',
            'AAAAAEEEEIIIIOOOOOUUUNCYaaaaaeeeeiiiiooooouuuncyy')),
        '[^a-z0-9]+', ' ', 'g'));

comment on function culina_fold_ae(text) is
    'Folds for comparison, expanding umlauts the way a German keyboard falls back to: ä becomes ae.';

create function culina_fold_a(value text) returns text
    language sql
    immutable
    strict
    parallel safe
    return btrim(regexp_replace(
        lower(translate(
            replace(replace(value, 'ß', 'ss'), 'ẞ', 'ss'),
            'ÄäÖöÜüÀÁÂÃÅÈÉÊËÌÍÎÏÒÓÔÕØÙÚÛÑÇÝàáâãåèéêëìíîïòóôõøùúûñçýÿ',
            'AaOoUuAAAAAEEEEIIIIOOOOOUUUNCYaaaaaeeeeiiiiooooouuuncyy')),
        '[^a-z0-9]+', ' ', 'g'));

comment on function culina_fold_a(text) is
    'Folds for comparison, stripping the diacritic the way unaccent does: ä becomes a.';

-- The terms the fuzzy lane looks for, in both folds.
--
-- Three characters is the floor, and it is about what the lane is for rather
-- than about cost: this lane answers typos and compounds, and a two-letter typo
-- carries no information while a two-letter substring matches most of a
-- library. Short words that genuinely name food — Ei, Öl — are found by the
-- full-text lane, which matches them exactly.
--
-- The stop list is short on purpose. It holds only the words that join two
-- nouns in a recipe search, because those are the ones that would otherwise
-- match every recipe in the household.
create function culina_search_terms(value text) returns text[]
    language sql
    immutable
    strict
    parallel safe
    return (
        select coalesce(array_agg(distinct word order by word), '{}'::text[])
        from unnest(
                 string_to_array(culina_fold_ae(value), ' ')
              || string_to_array(culina_fold_a(value), ' ')) as word
        where length(word) >= 3
          and word <> all (array[
              'und', 'oder', 'mit', 'ohne', 'aus', 'auf', 'fur', 'fuer', 'von', 'vom',
              'der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einen', 'einem',
              'ist', 'sind', 'was', 'wie', 'kann', 'ich', 'mir', 'sich', 'zum', 'zur',
              'the', 'and', 'for', 'with', 'without', 'from', 'that', 'this', 'what',
              'can', 'make', 'cook', 'recipe', 'rezept', 'rezepte'])
    );

comment on function culina_search_terms(text) is
    'The fuzzy lane''s query terms: folded both ways, de-duplicated, three characters or more, joining words dropped.';

-- ── Analyser version ────────────────────────────────────────────────────────
--
-- Bumped by any migration that changes how a document is built. The backfill at
-- the foot of that migration is then one statement — the same one this file
-- ends with — and it rewrites exactly the rows the change affected.
create function culina_search_analyzer_version() returns int
    language sql
    immutable
    parallel safe
    return 1;

comment on function culina_search_analyzer_version() is
    'The version of the document build. Raise it in the migration that changes the build, and re-run the backfill.';

-- ── The document ────────────────────────────────────────────────────────────

create table recipe_search_documents (
    recipe_id        uuid     not null primary key references recipes (id) on delete cascade,

    -- Denormalised from recipes.household_id so every search predicate can
    -- start here. Ownership is a security boundary, and a boundary reached
    -- through a join is a boundary somebody eventually forgets to join to.
    household_id     uuid     not null references households (id) on delete cascade,

    language         text     not null,

    -- A: title.  B: tags.  C: ingredient names and group names.
    -- D: description, ingredient notes and step text.
    document         tsvector not null,

    -- Every word of the recipe, in both folds, for the lane that answers
    -- compounds and typos. The second fold is appended only where it differs
    -- from the first, which is most of the cost of carrying two.
    fuzzy_text       text     not null,

    -- The title on its own, one column per fold. Two columns rather than one
    -- holding both spellings, because the exact lane tests equality and a
    -- single column reading 'kaesekuchen kasekuchen' equals neither spelling of
    -- anything anybody types.
    title_ae         text     not null,
    title_a          text     not null,

    -- Cached because the search reads it for every candidate — the ranking
    -- needs it, and so does the "uses 3 of 3 · 2 more needed" line. Counting
    -- it with a correlated subquery instead measured 620 ms over two thousand
    -- recipes, which was the single largest cost in the whole query and was
    -- there before any of this.
    ingredient_count int      not null default 0,

    analyzer_version int      not null
);

comment on table recipe_search_documents is
    'Derived, never authored. Written in the same transaction as the recipe, so it cannot describe a recipe that no longer says that.';

create index recipe_search_documents_document_idx
    on recipe_search_documents using gin (document);

create index recipe_search_documents_fuzzy_idx
    on recipe_search_documents using gin (fuzzy_text gin_trgm_ops);

-- text_pattern_ops states the intent rather than relying on it: the cluster
-- runs --locale=C today, where the default operator class would serve a prefix
-- LIKE anyway, and this keeps working if that ever changes.
create index recipe_search_documents_title_ae_idx
    on recipe_search_documents (household_id, title_ae text_pattern_ops);

create index recipe_search_documents_title_a_idx
    on recipe_search_documents (household_id, title_a text_pattern_ops);

-- Every search starts by naming a household, and every other index above is
-- reached through that filter.
create index recipe_search_documents_household_idx
    on recipe_search_documents (household_id);

-- Finds the rows an analyser change left behind, and nothing else.
create index recipe_search_documents_version_idx
    on recipe_search_documents (analyzer_version);

-- ── How a document is built ─────────────────────────────────────────────────
--
-- A view rather than a statement repeated three times. The per-recipe write,
-- the backfill below and every future re-index are the same INSERT over this
-- view with a different WHERE, so there is exactly one definition of what a
-- searchable recipe contains and no way for the three to drift apart.
--
-- It computes tsvectors, so it is expensive per row and is only ever selected
-- from by a write. Nothing on a read path may touch it.

create view recipe_search_input as
select
    r.id                                            as recipe_id,
    r.household_id,
    r.language,
    culina_fold_ae(r.title)                         as title_ae,
    culina_fold_a(r.title)                          as title_a,
    parts.ingredient_count,
    culina_search_analyzer_version()                as analyzer_version,

      setweight(to_tsvector(parts.config, r.title), 'A')
   || setweight(to_tsvector(parts.config, parts.tag_text), 'B')
   || setweight(to_tsvector(parts.config, parts.ingredient_text), 'C')
   || setweight(to_tsvector(parts.config, parts.body_text), 'D')
                                                    as document,

    -- The stripped fold is appended only when it says something the expanded
    -- one does not, which for a recipe without an umlaut in it is never.
    case
        when culina_fold_a(whole.all_text) = culina_fold_ae(whole.all_text)
            then culina_fold_ae(whole.all_text)
        else culina_fold_ae(whole.all_text) || ' ' || culina_fold_a(whole.all_text)
    end                                             as fuzzy_text
from recipes r
cross join lateral (
    select
        (case when r.language = 'de' then 'culina_de' else 'culina_en' end)::regconfig as config,

        coalesce((
            select string_agg(t.name || ' ' || replace(t.slug, '-', ' '), ' ' order by t.slug)
            from recipe_tags rt
            join tags t on t.id = rt.tag_id
            where rt.recipe_id = r.id), '')                                            as tag_text,

        coalesce((
            select string_agg(i.name, ' ' order by i.sort_order)
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            where g.recipe_id = r.id), '')
        || ' '
        || coalesce((
            select string_agg(g.name, ' ' order by g.sort_order)
            from ingredient_groups g
            where g.recipe_id = r.id and g.name is not null), '')                      as ingredient_text,

        -- The reference tokens are removed rather than folded away: a token's
        -- id is thirty-two hex characters, and the fold would keep every one of
        -- them as a word that matches queries nobody typed.
        coalesce(r.description, '')
        || ' '
        || coalesce((
            select string_agg(i.note, ' ' order by i.sort_order)
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            where g.recipe_id = r.id and i.note is not null), '')
        || ' '
        || coalesce((
            select string_agg(
                       regexp_replace(s.body, '\[\[ingredient:[^\]]*\]\]', ' ', 'g'),
                       ' ' order by s.sort_order)
            from steps s
            where s.recipe_id = r.id), '')                                             as body_text,

        (select count(*) from recipe_ingredients i
         join ingredient_groups g on g.id = i.group_id
         where g.recipe_id = r.id)::int                                                as ingredient_count
) parts
cross join lateral (
    select r.title || ' ' || parts.tag_text || ' ' || parts.ingredient_text
                   || ' ' || parts.body_text                                           as all_text
) whole;

comment on view recipe_search_input is
    'The one definition of a searchable recipe. Written through, never read from on a request path.';

-- ── Backfill ────────────────────────────────────────────────────────────────
--
-- Inside the migration rather than in a job at startup, so that the first
-- request after an upgrade searches a complete index. Roughly four seconds for
-- two thousand recipes, once.
--
-- This statement is also exactly what the application runs for one recipe and
-- what a future analyser change re-runs for the rows it invalidated. Keeping
-- the three identical is the point of the view above.
insert into recipe_search_documents (
    recipe_id, household_id, language, document, fuzzy_text,
    title_ae, title_a, ingredient_count, analyzer_version)
select
    recipe_id, household_id, language, document, fuzzy_text,
    title_ae, title_a, ingredient_count, analyzer_version
from recipe_search_input
on conflict (recipe_id) do update set
    household_id     = excluded.household_id,
    language         = excluded.language,
    document         = excluded.document,
    fuzzy_text       = excluded.fuzzy_text,
    title_ae         = excluded.title_ae,
    title_a          = excluded.title_a,
    ingredient_count = excluded.ingredient_count,
    analyzer_version = excluded.analyzer_version;
