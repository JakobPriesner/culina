-- A cookbook that fills itself.
--
-- The rules are stored; what matches them is not. A smart cookbook is a saved
-- question, answered whenever somebody looks — so a recipe written this evening
-- is on the right shelf the moment it is saved, with no job to run, nothing to
-- backfill when a rule changes, and no way for a membership table to drift out
-- of step with the recipes it claims to describe.
--
-- That is also why the rules live on the cookbook rather than in a table of
-- their own: there is nothing to join to. They are three small answers about
-- one shelf, read every single time that shelf is, and a child table would be
-- a join on every read to reassemble one row's worth of question.
alter table cookbooks
    add column kind              text   not null default 'manual',
    add column rule_tags         text[] not null default '{}',
    add column rule_ingredients  text[] not null default '{}',
    add column rule_max_minutes  int;

comment on column cookbooks.kind is
    'manual: what is on it is in cookbook_recipes. smart: what is on it is whatever matches the rules, worked out at read time.';

comment on column cookbooks.rule_tags is
    'Tag slugs, all of which a recipe must carry. The same meaning repeated ?tag= already has on the recipe search, so a shelf and the filter bar cannot disagree about what a tag rule means.';

comment on column cookbooks.rule_ingredients is
    'Ingredient names, all of which a recipe must use. A harder rule than the ingredient search, which ranks rather than excludes: on a shelf, "chicken" has to mean only chicken.';

-- Text, not an integer, for the reason the meal plan's slot is: a migration
-- that inserted a kind in the middle could not silently reassign every row, and
-- somebody reading the table can see what it says.
alter table cookbooks
    add constraint cookbooks_kind_known check (kind in ('manual', 'smart'));

-- The two halves of "a cookbook is one or the other". A manual shelf carrying
-- rules nothing reads, or a smart shelf carrying none, are both rows that would
-- make the next reader ask which of the two answers is true.
alter table cookbooks
    add constraint cookbooks_manual_states_no_rules check (
        kind = 'smart'
        or (cardinality(rule_tags) = 0
            and cardinality(rule_ingredients) = 0
            and rule_max_minutes is null));

alter table cookbooks
    add constraint cookbooks_smart_states_a_rule check (
        kind = 'manual'
        or cardinality(rule_tags) > 0
        or cardinality(rule_ingredients) > 0
        or rule_max_minutes is not null);

alter table cookbooks
    add constraint cookbooks_rule_minutes_positive
        check (rule_max_minutes is null or rule_max_minutes > 0);

-- Nothing is ever written to cookbook_recipes for a smart shelf, so no index
-- changes: the rules are evaluated against recipes, and the indexes that
-- already serve the recipe search serve them too.
