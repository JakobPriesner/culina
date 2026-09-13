-- What this household means to cook this week.
--
-- Household-owned like recipes, and deliberately not a calendar: a week is the
-- unit people actually plan in, and a month view is a feature that arrives with
-- recurrence, drag-and-drop and a second reason for a shopping list to exist.
create table meal_plan_entries (
    id           uuid           not null primary key,
    household_id uuid           not null references households (id) on delete cascade,

    -- A date, not a timestamp. "Tuesday" has no time zone, and storing one
    -- would move somebody's dinner when they travelled.
    on_date      date           not null,

    recipe_id    uuid           not null references recipes (id) on delete cascade,

    -- Null means "however many it was written for". Most planned meals are
    -- cooked as written, and asking every time is a question with an obvious
    -- answer.
    servings     numeric(10, 3),

    slot         text           not null,
    sort_order   int            not null default 0,

    constraint meal_plan_servings_positive check (servings is null or servings > 0)
);

comment on column meal_plan_entries.recipe_id is
    'on delete cascade: a recipe that no longer exists cannot be cooked on Thursday, and a plan entry pointing at nothing is worse than a gap.';

-- The week view is the only read: one household, one range of dates.
create index meal_plan_by_week on meal_plan_entries (household_id, on_date, sort_order);
