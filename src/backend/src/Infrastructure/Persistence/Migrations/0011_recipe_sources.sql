-- Where a household's recipes came from, and how to go back for more.
--
-- Two tables for two different questions, and they are genuinely different.
-- A *source* is a place that is still there: a Tandoor instance on the shelf
-- next to this one, with a token, that will have more recipes next month. An
-- *origin* is a fact about one recipe that never changes: this one came from
-- over there, and here is the id it had.
--
-- Keeping them apart is what makes disconnecting a source safe. Pulling the
-- plug on an instance must not make the recipes it gave you forget where they
-- came from, and it certainly must not delete them.
create table recipe_sources (
    id           uuid        not null primary key,
    household_id uuid        not null references households (id) on delete cascade,

    kind         text        not null,
    label        text        not null,
    base_url     text        not null,

    -- The API token, stored because the whole point of a connection is coming
    -- back to it without being asked again. It never leaves the server: no
    -- response contains this column, and no endpoint returns it.
    secret       text        not null,

    created_by   uuid        not null references users (id) on delete restrict,
    created_at   timestamptz not null,
    -- When this source was last read from. Null until the first import, which
    -- is the difference between "connected" and "used".
    last_used_at timestamptz,
    version      bigint      not null default 1
);

comment on table recipe_sources is
    'A recipe library elsewhere that this household has connected: its address and its credential. One row per connection, not per import — the point of remembering it is coming back for what is new.';

comment on column recipe_sources.kind is
    'Which strategy reads it. Adding a source type adds a value here and a class in Infrastructure, and nothing else.';

comment on column recipe_sources.base_url is
    'The origin of the other app, normalised to scheme://host[:port]. Stored without a path so a mistyped /api or trailing slash cannot become part of every request this ever makes.';

-- Text, as cookbooks.kind is: a migration that inserted a value in the middle
-- of an integer enum could not silently reassign every row, and somebody
-- reading the table can see what it says.
alter table recipe_sources
    add constraint recipe_sources_kind_known check (kind in ('tandoor'));

-- One connection per address per household. Connecting the same instance twice
-- is a mistake every time — two rows, two tokens, and a browse that shows you
-- your own library side by side with itself.
create unique index recipe_sources_one_per_address_idx
    on recipe_sources (household_id, kind, base_url);

-- The only read: what has this kitchen connected?
create index recipe_sources_household_idx on recipe_sources (household_id, created_at);

-- Where one recipe came from.
--
-- A table rather than columns on `recipes`, because provenance is a property of
-- a few recipes and not of all of them. Most recipes are written here and have
-- no origin at all, and four mostly-null columns on the widest, most-read table
-- in the schema is a cost every recipe page pays for a fact that belongs to a
-- minority of them.
create table recipe_origins (
    recipe_id    uuid        not null primary key references recipes (id) on delete cascade,
    -- Carried here as well as on the recipe, so the uniqueness rule below can
    -- be enforced by the database rather than by whoever remembers to check.
    household_id uuid        not null references households (id) on delete cascade,

    kind         text        not null,
    -- Which connection brought it over, when one did. Null for a recipe read
    -- from a public page, and null again once a source is disconnected.
    source_id    uuid                 references recipe_sources (id) on delete set null,

    -- What the other app called it. A string, because Tandoor counts and a
    -- website does not: a URL is as good an identity as an integer, and this
    -- column should not have an opinion about which.
    external_id  text        not null,
    -- Where to go and look at the original. Shown on the recipe.
    source_url   text,
    imported_at  timestamptz not null
);

comment on table recipe_origins is
    'Where an imported recipe came from. Survives disconnecting the source: pulling the plug on an instance must not make the recipes it gave you forget where they came from.';

comment on column recipe_origins.source_id is
    'on delete set null, deliberately. A disconnected source leaves its recipes exactly where they are, still knowing which app and which id they came from.';

alter table recipe_origins
    add constraint recipe_origins_kind_known check (kind in ('tandoor', 'web'));

-- The idempotency key, and the single most load-bearing line in this file.
--
-- Importing is not a one-off. People run it, add recipes over there, and come
-- back for the rest — and a retried request, a double tap, and a second run a
-- month later must all mean the same thing: bring over what is not here yet.
-- With this, "have I already got this one?" is a question the database answers,
-- rather than a check somebody has to remember to write.
create unique index recipe_origins_once_per_household_idx
    on recipe_origins (household_id, kind, external_id);
