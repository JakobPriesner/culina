-- Ingredient names, folded once when they are written rather than on every
-- keystroke.
--
-- Completing a half-typed search asks which of a household's ingredient names
-- has a word beginning with what was typed, in either German fold. Folding
-- every name at query time measured 75 ms at the 95th percentile over two
-- thousand recipes — sixteen thousand names, each folded twice, for a request
-- that is asked on every pause in typing and has forty. The titles already
-- carry their folds on the search document (0012); this gives the names the
-- same, as generated columns so that nothing that writes an ingredient can
-- forget them, and a trigram index so "% word" is an index scan.
alter table recipe_ingredients
    add column name_ae text generated always as (culina_fold_ae(name)) stored,
    add column name_a  text generated always as (culina_fold_a(name)) stored;

create index recipe_ingredients_name_ae_trgm_idx
    on recipe_ingredients using gin (name_ae gin_trgm_ops);

create index recipe_ingredients_name_a_trgm_idx
    on recipe_ingredients using gin (name_a gin_trgm_ops);
