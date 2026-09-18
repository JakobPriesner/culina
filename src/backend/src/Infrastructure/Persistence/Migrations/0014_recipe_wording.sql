-- What a recipe calls its own yield, and what it calls its own steps.
--
-- Both are nullable and both mean the same thing when they are: say it the way
-- the app has always said it. A recipe that never asks for its own words is
-- byte-for-byte the recipe it was before this migration ran.

alter table recipes add column yield_label text;

comment on column recipes.yield_label is
    'The noun this recipe measures itself in — "Cake", "Gläser", "Blech". Null means the word is derived from yield_kind, which is what nearly every recipe wants. It overrides the wording only; yield_kind still decides how the servings stepper counts, because "makes one cake" and "goes up by one" are different questions.';

alter table steps add column title text;

comment on column steps.title is
    'What this step is called, when it is called anything. Null means it is shown as "Step N", which is what a step in a short recipe is. Stored per step rather than as a grouping table: a heading here is a name for one step, not a section spanning several.';
