-- Foods a household typed from a package's label.
--
-- The Bundeslebensmittelschlüssel has no light coconut milk, Skyr light or oat
-- drink, so a line naming one could only ever be "not counted". A household
-- can type the label's values per 100 g instead, cited as "from the package",
-- and choose that food for an ingredient name in the correction sheet. The
-- choice is stored in nutrition_food_overrides as the code 'h:' followed by
-- the id without dashes, which no BLS code (a letter and six digits) can be.
--
-- A food is not edited, only added and removed, so a correction's code alone
-- says which values were counted. Null is unknown, never zero, as in BLS.
create table household_foods (
    id             uuid        primary key,
    household_id   uuid        not null references households_with_deleted (id) on delete cascade,
    name           text        not null,
    energy_kj      numeric     not null,
    energy_kcal    numeric     not null,
    fat            numeric,
    saturated_fat  numeric,
    carbohydrate   numeric,
    sugars         numeric,
    protein        numeric,
    salt           numeric,
    created_at     timestamptz not null
);

create index household_foods_household on household_foods (household_id);

comment on table household_foods is
    'Foods a household typed from a package label, per 100 g; chosen for an ingredient like a BLS food.';
