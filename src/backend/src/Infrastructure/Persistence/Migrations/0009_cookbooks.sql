-- Cookbooks: a household's own named shelves of recipes.
--
-- A playlist, not a genre. A tag is a property of a recipe and classifies it;
-- a cookbook is a curation, with a name written in prose, a description and a
-- page of its own, and being on one says nothing about the recipe itself. A
-- recipe is *tagged* vegetarian; it is *in* Sunday roasts.
--
-- Household-owned, like the recipes it points at. A private curation of shared
-- recipes breaks the moment somebody leaves the household, and the axis this
-- model keeps apart is "what everyone here edits" from "what one person
-- thinks" — a shelf of the kitchen's own recipes is the first kind.
create table cookbooks (
    id           uuid        not null primary key,
    household_id uuid        not null references households (id) on delete cascade,
    name         text        not null,
    description  text,

    created_by   uuid        not null references users (id) on delete restrict,
    created_at   timestamptz not null,
    updated_at   timestamptz not null,
    version      bigint      not null default 1
);

comment on table cookbooks is
    'A household-owned, named curation of recipes. This row is the shelf''s own metadata only; what is on it lives in cookbook_recipes.';

comment on column cookbooks.created_by is
    'on delete restrict, copying recipes.created_by: deleting an account must not silently delete a cookbook the rest of the household still cooks from.';

comment on column cookbooks.version is
    'Bumped by a rename and by every membership change, because the recipe count and the cover mosaic are part of what this row''s ETag answers for.';

-- The only read of the household's shelves: most recently changed first, a
-- page at a time.
create index cookbooks_household_updated_idx on cookbooks (household_id, updated_at desc, id desc);

-- What is on one shelf. Nothing else: a cookbook owns no recipe content, so
-- this table is the whole of the relationship.
create table cookbook_recipes (
    cookbook_id uuid        not null references cookbooks (id) on delete cascade,
    recipe_id   uuid        not null references recipes (id) on delete cascade,
    added_at    timestamptz not null,
    added_by    uuid                 references users (id) on delete set null,

    -- Adding a recipe that is already on the shelf has to be a no-op rather
    -- than a second row or an error: a double tap and a retried request are
    -- both ordinary, and neither means "put it on twice".
    primary key (cookbook_id, recipe_id)
);

comment on table cookbook_recipes is
    'The recipes on one shelf. recipe_id cascades, as meal_plan_entries.recipe_id does: a deleted recipe drops off every shelf it was on, rather than leaving a cookbook pointing at nothing. The reverse is deliberately not true — deleting a cookbook deletes no food.';

comment on column cookbook_recipes.added_by is
    'Attribution only, so on delete set null: losing who put a recipe on a shelf is a smaller loss than refusing to delete an account over a join row nobody reads for identity.';

-- Reading one shelf in its own order, which is the order things were put on
-- it. The primary key does not serve this: within a cookbook it orders by
-- recipe id, which is no order a person chose.
create index cookbook_recipes_in_order_idx on cookbook_recipes (cookbook_id, added_at, recipe_id);

-- The other direction, asked on every recipe page: which shelves is this one
-- on? Without this it is a sequential scan of every membership in the install.
create index cookbook_recipes_by_recipe_idx on cookbook_recipes (recipe_id);
