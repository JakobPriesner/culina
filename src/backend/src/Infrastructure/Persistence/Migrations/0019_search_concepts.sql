-- The concept lane: what a recipe is, as well as what it says.
--
-- The lexical lanes find the words a recipe contains. They cannot find that
-- Waffeln are a dessert, that Hähnchen is chicken to somebody typing in
-- English, or that a Bolognese is Italian — none of those words is in the
-- recipe. Domain/Search/CulinaryLexicon says so instead, and this column holds
-- its answer: the concepts a recipe's title, tags and ingredients name, with
-- everything they are a kind of already expanded, so that matching is an
-- array overlap an index can serve rather than a walk up a hierarchy.
--
-- Written by the application rather than by recipe_search_input, and that is
-- the one place this document departs from "the database builds it". The
-- lexicon is three hundred entries of C# with a unit test against every one;
-- the same table in SQL would be a second copy of it, and the two would
-- disagree the first time somebody fixed one.
--
-- lexicon_version says which lexicon built the row. Every row starts at zero,
-- which no lexicon is, so the reindex that runs when the application starts
-- (LexiconReindexService) fills them all in before the first request — the
-- same place 0012's backfill ran for the rest of the document.

alter table recipe_search_documents
    add column concepts        text[] not null default '{}',
    add column lexicon_version int    not null default 0;

comment on column recipe_search_documents.concepts is
    'Lexicon concepts of the title, tags and ingredients, with their ancestors. Written by the application.';

create index recipe_search_documents_concepts_idx
    on recipe_search_documents using gin (concepts);

-- Finds the rows a lexicon change left behind, and nothing else.
create index recipe_search_documents_lexicon_idx
    on recipe_search_documents (lexicon_version);
