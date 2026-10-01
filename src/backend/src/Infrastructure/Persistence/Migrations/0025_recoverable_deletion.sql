-- Deleting a household, a recipe or a cookbook puts it in the bin for thirty
-- days instead of removing it, so a mistake — or one member's delete in a
-- shared kitchen — can be undone. A background job purges what is older.
--
-- The tables are renamed to *_with_deleted and a view takes each old name,
-- showing only what has not been deleted. Every query that already reads
-- `recipes`, `cookbooks` or `households` therefore excludes deleted rows
-- without being touched: one predicate, in one place, rather than a
-- `deleted_at is null` every statement would have to remember. Only the code
-- that deletes, restores, lists the bin and purges reads the tables directly.
--
-- The views are simple enough for PostgreSQL to keep them updatable: insert
-- (with the table's defaults), update, `on conflict` and `returning` all pass
-- through to the table. Foreign keys keep pointing at the tables, so a purge
-- still cascades exactly as a delete used to.
--
-- A recipe or cookbook of a deleted household is hidden with it, while its
-- own deleted_at stays null: restoring the household brings back exactly what
-- it had, and nothing deleted earlier comes back with it.
--
-- A view's column list is fixed when it is created. A later migration that
-- adds a column to one of these tables must create the view again.

alter table households rename to households_with_deleted;
alter table households_with_deleted
    add column deleted_at timestamptz,
    add column deleted_by uuid references users (id) on delete set null;

alter table recipes rename to recipes_with_deleted;
alter table recipes_with_deleted
    add column deleted_at timestamptz,
    add column deleted_by uuid references users (id) on delete set null;

alter table cookbooks rename to cookbooks_with_deleted;
alter table cookbooks_with_deleted
    add column deleted_at timestamptz,
    add column deleted_by uuid references users (id) on delete set null;

create view households as
select * from households_with_deleted
where deleted_at is null;

create view recipes as
select * from recipes_with_deleted r
where r.deleted_at is null
  and exists (select 1 from households h where h.id = r.household_id);

create view cookbooks as
select * from cookbooks_with_deleted c
where c.deleted_at is null
  and exists (select 1 from households h where h.id = c.household_id);

-- The bin and the purge look for deleted rows, which are few; partial, so the
-- live rows every other query reads cost the index nothing.
create index households_deleted_at_idx on households_with_deleted (deleted_at)
    where deleted_at is not null;
create index recipes_deleted_at_idx on recipes_with_deleted (household_id, deleted_at)
    where deleted_at is not null;
create index cookbooks_deleted_at_idx on cookbooks_with_deleted (household_id, deleted_at)
    where deleted_at is not null;
