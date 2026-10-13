-- Which recipes the front page showed a person, one row per recipe per day.
--
-- It pays for one behaviour a deterministic ranker cannot have without it: "do
-- not show me the same five recipes every evening this week". Without a memory
-- of what was shown, the top five stay the top five until somebody cooks one.
-- Metrics are a side effect, not the reason (docs/suggestions-research.md C.4).
--
-- A day, not an instant: the list is fixed for the day, so what matters is that
-- it was shown on a day before today. Rows for today never count, which keeps
-- the list stable across refreshes and devices all evening.
create table suggestion_impressions (
    user_id   uuid not null references users (id) on delete cascade,
    recipe_id uuid not null references recipes_with_deleted (id) on delete cascade,
    shown_on  date not null,

    -- A fact at a known address, not an event: showing the same recipe twice
    -- on one day is one impression, so a refresh or a second device writes
    -- nothing new.
    primary key (user_id, recipe_id, shown_on)
);

comment on table suggestion_impressions is
    'Person-owned and never exported. What the front page showed one person, by day; read only to push down what they saw recently. Swept to 90 days whenever that person is shown something.';

-- Serves the ranker: everything one person was shown, newest first.
create index suggestion_impressions_user_day on suggestion_impressions (user_id, shown_on desc);

-- No version column: never read-modify-written, so nothing to lose and nothing
-- to carry an ETag.
