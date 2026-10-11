-- What a household says one unit of an ingredient weighs, for nutrition, and
-- whether typical weights may count its lines at all.
--
-- "Bei uns wiegt 1 Zwiebel 150 g": per household, per ingredient name and unit,
-- one tap from the nutrition breakdown. name_key is the folded name, the key
-- nutrition_food_overrides uses; unit_key is the canonical unit (piece, clove,
-- tbsp ... or a household's own word, folded), so "Stk" and "Stück" share a row.
-- The row is an idempotent fact at a known address, so there is no version
-- column; the nutrition ETag fingerprints the rows that apply.
create table nutrition_unit_weights (
    household_id uuid           not null references households_with_deleted (id) on delete cascade,
    name_key     text           not null,
    unit_key     text           not null,
    grams        numeric(10, 3) not null check (grams > 0),
    updated_at   timestamptz    not null,

    primary key (household_id, name_key, unit_key)
);

-- A household that never said anything has no row, and typical weights count:
-- the absence of a row is the default, so a new household needs no write.
create table nutrition_household_settings (
    household_id        uuid        not null primary key references households_with_deleted (id) on delete cascade,
    use_typical_weights boolean     not null,
    updated_at          timestamptz not null
);
