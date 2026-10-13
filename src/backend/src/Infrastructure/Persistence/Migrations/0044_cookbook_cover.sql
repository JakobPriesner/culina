-- A cookbook's cover, when somebody chose one.
--
-- Until now the cover was always derived: the first four photographed recipes
-- on the shelf, in shelf order. That stays the default (null). A chosen cover is
-- one recipe whose photograph becomes the whole cover, shown only while that
-- recipe is on the shelf and has a photograph; otherwise the derived cover
-- comes back on its own, so nothing has to be cleaned up when it is taken off,
-- loses its picture, or stops matching a smart cookbook's rules.
--
-- On delete set null: the recipe row going is the same as it leaving the shelf.
-- A recipe in the bin keeps its row, and the read rule above already hides it.
alter table cookbooks_with_deleted
    add column cover_recipe_id uuid references recipes_with_deleted (id) on delete set null;

comment on column cookbooks_with_deleted.cover_recipe_id is
    'The recipe whose photograph is the whole cover, or null for the first photographs on the shelf. Ignored while that recipe is not on the shelf or has no photograph.';

-- A view's column list is fixed when it is created (0025, 0028), so cookbooks is
-- created again to show the new column.
drop view cookbooks;

create view cookbooks as
select * from cookbooks_with_deleted c
where c.deleted_at is null
  and exists (select 1 from households h where h.id = c.household_id);
