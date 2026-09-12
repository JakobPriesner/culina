-- What one person thinks about a recipe, as opposed to what the household
-- stores. Both tables are keyed by user as well as recipe, which is the whole
-- point: two people can disagree about a recipe without either editing it.

create table personal_notes (
    id         uuid        not null primary key,
    recipe_id  uuid        not null references recipes (id) on delete cascade,
    user_id    uuid        not null references users (id) on delete cascade,
    -- Null for a note about the recipe as a whole.
    step_id    uuid        references steps (id) on delete cascade,
    body       text        not null,
    updated_at timestamptz not null,
    version    bigint      not null default 1
);

-- One note per person per place. A partial unique index is needed because
-- PostgreSQL treats nulls as distinct, so a plain unique constraint would let
-- one person write many recipe-level notes.
create unique index personal_notes_recipe_user_step_idx
    on personal_notes (recipe_id, user_id, step_id)
    where step_id is not null;

create unique index personal_notes_recipe_user_overall_idx
    on personal_notes (recipe_id, user_id)
    where step_id is null;

create table cook_log_entries (
    id           uuid        not null primary key,
    recipe_id    uuid        not null references recipes (id) on delete cascade,
    user_id      uuid        not null references users (id) on delete cascade,
    household_id uuid        not null references households (id) on delete cascade,
    made_at      timestamptz not null,
    servings     numeric(10, 3),
    note         text
);

comment on table cook_log_entries is
    'Append-only. What someone actually cooked is a better signal than a star rating nobody maintains, which is why Culina has no ratings.';

create index cook_log_recipe_user_idx on cook_log_entries (recipe_id, user_id, made_at desc);
create index cook_log_user_idx on cook_log_entries (user_id, made_at desc);
