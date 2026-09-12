-- Cooking sessions: where somebody is in a recipe, right now.
--
-- Person-owned, and at most one active per person. That invariant is what makes
-- "what am I cooking?" a single unambiguous answer, and it is enforced here
-- rather than in application code, because two devices starting a session at
-- the same moment is exactly the case application code gets wrong.

create table cook_sessions (
    id                  uuid primary key,
    recipe_id           uuid        not null references recipes (id) on delete cascade,
    user_id             uuid        not null references users (id) on delete cascade,
    household_id        uuid        not null references households (id) on delete cascade,

    -- The scaling in force. Coming back to a recipe you had scaled to six and
    -- finding it at four is worse than not remembering at all.
    servings            numeric(10, 3) not null,
    current_step_index  integer     not null default 0,

    started_at          timestamptz not null,
    last_active_at      timestamptz not null,
    completed_at        timestamptz,
    abandoned_at        timestamptz,
    version             bigint      not null default 1,

    constraint cook_sessions_servings_positive check (servings > 0),
    constraint cook_sessions_step_not_negative check (current_step_index >= 0),
    -- A session cannot be both finished and given up on.
    constraint cook_sessions_one_ending check (completed_at is null or abandoned_at is null)
);

-- The invariant, in the only place that can actually hold it.
create unique index cook_sessions_one_active_per_user
    on cook_sessions (user_id)
    where completed_at is null and abandoned_at is null;

-- Resuming reads by user; the history view reads by recipe.
create index cook_sessions_by_recipe on cook_sessions (recipe_id, user_id);
