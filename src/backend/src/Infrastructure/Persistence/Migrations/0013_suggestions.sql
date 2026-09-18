-- What somebody said "not this" to.
--
-- The only genuinely new signal the suggestion ranker needs. Everything else it
-- reads — the cook log, the plan, the shelves, the notes — is already written
-- by the handler that owns it and then never read again.
--
-- It has to be collected explicitly rather than inferred, because with two to
-- eight people there is no such thing as a statistically meaningful non-click:
-- five suggestions ignored on one evening is one data point about one evening,
-- and reading dislike into that would be manufacturing data.
create table suggestion_dismissals (
    user_id      uuid        not null references users (id) on delete cascade,
    recipe_id    uuid        not null references recipes (id) on delete cascade,
    dismissed_at timestamptz not null,

    -- A fact at a known address, not an event: dismissing twice is one
    -- dismissal. The composite key is what makes a double tap and a retried
    -- request both ordinary, the same way cookbook_recipes does it.
    primary key (user_id, recipe_id)
);

comment on table suggestion_dismissals is
    'Person-owned. One person hiding a recipe from their own suggestions says nothing about anybody else in the household, and the recipe itself is untouched.';

comment on column suggestion_dismissals.dismissed_at is
    'Dismissals expire: the ranker ignores rows older than its window, so "not tonight" does not silently become "never again".';

-- The only read: everything this person has hidden, while scoring. Small, and
-- the primary key already serves it.

-- No version column, and deliberately so. This is not an aggregate root: it is
-- never read-modify-written, so it has no concurrency to lose and nothing that
-- could sensibly carry an ETag.
