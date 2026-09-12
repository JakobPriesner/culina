-- Recipes, their ingredients and steps, and the tags they carry.

create table recipes (
    id           uuid           not null primary key,
    household_id uuid           not null references households (id) on delete cascade,
    title        text           not null,
    description  text,
    language     text           not null default 'en',
    yield_amount numeric(10, 3) not null,
    yield_kind   text           not null,
    prep_minutes int,
    cook_minutes int,
    image_id     uuid,
    created_by   uuid           not null references users (id) on delete restrict,
    created_at   timestamptz    not null,
    updated_at   timestamptz    not null,
    version      bigint         not null default 1
);

comment on column recipes.created_by is
    'on delete restrict, not cascade: deleting an account must not silently delete recipes the rest of the household still cooks from.';

create index recipes_household_updated_idx on recipes (household_id, updated_at desc);
create index recipes_household_title_idx on recipes (household_id, title);

-- Trigram indexes back the free-text search. Without them a LIKE '%...%' over
-- titles and ingredient names is a sequential scan of the whole household.
create index recipes_title_trgm_idx on recipes using gin (title gin_trgm_ops);

create table ingredient_groups (
    id         uuid not null primary key,
    recipe_id  uuid not null references recipes (id) on delete cascade,
    name       text,
    sort_order int  not null
);

comment on column ingredient_groups.name is
    'Null for the implicit first group, which is what keeps grouping invisible until a recipe actually uses it.';

create index ingredient_groups_recipe_idx on ingredient_groups (recipe_id, sort_order);

create table recipe_ingredients (
    id         uuid           not null primary key,
    group_id   uuid           not null references ingredient_groups (id) on delete cascade,
    sort_order int            not null,
    -- numeric, never float: 0.1 litres is an ordinary thing for a recipe to
    -- ask for and binary floating point cannot represent it.
    quantity   numeric(10, 3),
    unit       text,
    name       text           not null,
    note       text
);

comment on column recipe_ingredients.note is
    'Preparation ("finely chopped"), kept out of name so two butters can merge into one shopping-list line.';

create index recipe_ingredients_group_idx on recipe_ingredients (group_id, sort_order);
create index recipe_ingredients_name_trgm_idx on recipe_ingredients using gin (name gin_trgm_ops);

create table steps (
    id               uuid not null primary key,
    recipe_id        uuid not null references recipes (id) on delete cascade,
    sort_order       int  not null,
    body             text not null,
    duration_seconds int
);

comment on column steps.body is
    'Text with inline [[ingredient:<id>]] tokens. The token form never leaves the database; the API exposes segments.';

create index steps_recipe_idx on steps (recipe_id, sort_order);

-- Derived from the step bodies on every save. It exists so "which steps use
-- the yeast?" is an indexed lookup rather than a LIKE scan.
create table step_ingredient_refs (
    step_id              uuid not null references steps (id) on delete cascade,
    recipe_ingredient_id uuid not null references recipe_ingredients (id) on delete cascade,
    primary key (step_id, recipe_ingredient_id)
);

create index step_ingredient_refs_ingredient_idx
    on step_ingredient_refs (recipe_ingredient_id);

create table tags (
    id           uuid not null primary key,
    household_id uuid not null references households (id) on delete cascade,
    name         text not null,
    slug         text not null,
    unique (household_id, slug)
);

comment on table tags is
    'Household-scoped, so each household keeps its own vocabulary and nothing leaks between them.';

create table recipe_tags (
    recipe_id uuid not null references recipes (id) on delete cascade,
    tag_id    uuid not null references tags (id) on delete cascade,
    primary key (recipe_id, tag_id)
);

create index recipe_tags_tag_idx on recipe_tags (tag_id);

create table recipe_images (
    id           uuid        not null primary key,
    recipe_id    uuid        not null references recipes (id) on delete cascade,
    content_hash text        not null,
    width        int         not null,
    height       int         not null,
    byte_size    int         not null,
    content_type text        not null,
    created_at   timestamptz not null
);

create index recipe_images_recipe_idx on recipe_images (recipe_id);
