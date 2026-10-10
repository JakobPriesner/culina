-- What a household says an ingredient really is, for nutrition.
--
-- The name-to-food guess is right most of the time; the one time it is not, one
-- tap fixes it for every recipe of the household, like the shopping section
-- override. name_key is the folded name, the same key that table uses.
--
-- food_code null means "do not count this". No version column: a row is an
-- idempotent fact at a known address, and the nutrition ETag fingerprints the
-- rows that apply.
create table nutrition_food_overrides (
    household_id uuid        not null references households_with_deleted (id) on delete cascade,
    name_key     text        not null,
    food_code    text        null,
    updated_at   timestamptz not null,

    primary key (household_id, name_key)
);
