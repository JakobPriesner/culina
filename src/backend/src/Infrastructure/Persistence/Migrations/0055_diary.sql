-- A private food diary (docs/diary-design.md): what one person ate, by day and meal.
--
-- Person-owned like the cook log: it belongs to a user, never to a household, and goes
-- with the account. An entry keeps the figures it was logged with, so editing or binning
-- a recipe later never changes what someone ate on Monday.

create table diary_settings (
    user_id         uuid        not null primary key references users (id) on delete cascade,
    enabled         boolean     not null default false,
    consented_at    timestamptz,
    consent_version int,
    kcal_target     int,
    protein_target  int,
    headline        text        not null default 'eaten' check (headline in ('eaten', 'remaining')),
    version         bigint      not null default 1,
    updated_at      timestamptz not null
);

comment on table diary_settings is
    'One row per person who ever opened the diary settings; none means switched off. consented_at and consent_version record when, and to which wording, the person agreed that their entries are kept as health data.';

create table diary_entries (
    id                uuid          not null primary key,
    user_id           uuid          not null references users (id) on delete cascade,
    on_date           date          not null,
    meal              text          not null check (meal in ('breakfast', 'lunch', 'dinner', 'snack')),
    source            text          not null check (source in ('recipe', 'food', 'typed')),
    recipe_id         uuid,
    household_id      uuid,
    food_code         text,
    cook_log_entry_id uuid,
    amount            numeric(10, 3),
    unit              text,
    grams             numeric(10, 3),
    label             text          not null,
    kcal              numeric(10, 3),
    protein_g         numeric(10, 3),
    carbs_g           numeric(10, 3),
    fat_g             numeric(10, 3),
    sat_fat_g         numeric(10, 3),
    sugars_g          numeric(10, 3),
    salt_g            numeric(10, 3),
    at_least          boolean       not null default false,
    estimated         boolean       not null default false,
    basis             text,
    nutrition_version int           not null,
    created_at        timestamptz   not null,
    version           bigint        not null default 1
);

comment on table diary_entries is
    'What one person ate. The figures are a snapshot taken when logged (null is unknown, never zero); recipe_id, household_id, food_code and cook_log_entry_id are soft links that may outlive what they point at.';

create index diary_entries_user_day_idx on diary_entries (user_id, on_date);
create index diary_entries_cook_log_idx on diary_entries (cook_log_entry_id) where cook_log_entry_id is not null;
