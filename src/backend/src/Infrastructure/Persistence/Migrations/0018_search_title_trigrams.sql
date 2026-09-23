-- The typo lane, answered from an index.
--
-- Every other lane of recipe search already had an index, and the typo lane
-- was the one that could not: it asks how nearly each title resembles each word
-- of the query, and asked that way it measures every title in the household.
-- Measured over ten thousand recipes that was 14 ms for one word and 35 ms for
-- three, which made it the largest cost left once the others were indexed.
--
-- A trigram index on the title lets the `<%` operator find the titles close
-- enough to a word without measuring the rest. `<%` reads its threshold from
-- pg_trgm.word_similarity_threshold rather than taking one, so the connection
-- sets it (CulinaDataSource), from the same constant the search compares
-- against (RecipeSearcher.FuzzyThreshold). The search still compares the
-- similarity itself, so the setting decides only what the index hands back,
-- never what counts as a match.
--
-- title_ae only, because that is the only spelling the typo lane measures.
create index recipe_search_documents_title_ae_trgm_idx
    on recipe_search_documents using gin (title_ae gin_trgm_ops);
