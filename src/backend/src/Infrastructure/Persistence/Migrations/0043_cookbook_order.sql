-- The order of a chosen shelf, as somebody arranged it.
--
-- Until now a shelf read in the order its recipes were put on it (added_at).
-- That stays the starting point: every existing shelf is numbered in exactly
-- that order, so nothing moves until somebody moves it. added_at keeps meaning
-- "when it went on", which the suggestion ranker reads, so moving a recipe is a
-- second column rather than a rewritten date.
--
-- No unique constraint on (cookbook_id, sort_order): two recipes added at the
-- same moment may share a number, and the recipe id breaks the tie as it does
-- everywhere else. A reorder rewrites every number on the shelf at once.
alter table cookbook_recipes add column sort_order int;

update cookbook_recipes cr
set sort_order = numbered.n
from (
    select cookbook_id, recipe_id,
           row_number() over (partition by cookbook_id order by added_at, recipe_id) as n
    from cookbook_recipes
) as numbered
where numbered.cookbook_id = cr.cookbook_id
  and numbered.recipe_id = cr.recipe_id;

alter table cookbook_recipes alter column sort_order set not null;

comment on column cookbook_recipes.sort_order is
    'Where the recipe stands on the shelf, from 1. A new recipe goes last; PUT /cookbooks/{id}/recipes renumbers the whole shelf.';

drop index cookbook_recipes_in_order_idx;

create index cookbook_recipes_in_order_idx on cookbook_recipes (cookbook_id, sort_order, recipe_id);
