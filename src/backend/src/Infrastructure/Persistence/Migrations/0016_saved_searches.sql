-- A search somebody wants back.
--
-- The library's toolbar holds four things — words, tags, a time ceiling and an
-- order — and this table is those four things with a name on them. Saving is
-- therefore a copy rather than a translation, which is the only reason a saved
-- search can be trusted to reopen as the search that was saved.
--
-- Deliberately not a smart cookbook, although docs/search-design.md 18.8 says a
-- smart cookbook is a saved search. The obstacle is concrete: a cookbook card's
-- count and cover come from SmartShelfSql.Matches, evaluated once per card, and
-- tags, ingredients and a time ceiling are cheap predicates there while a
-- free-text rule is the four-lane relevance engine. So a shelf stays a curation
-- — counted, covered, shoppable — and a search stays a lens, ordered and fuzzy.
-- The two are joined in the interface instead: a saved search whose dimensions
-- a shelf can hold offers to become one.
create table saved_searches (
    id           uuid        not null primary key,
    household_id uuid        not null references households (id) on delete cascade,
    name         text        not null,

    -- The four dimensions, stored the way the query string carries them, so
    -- applying a saved search is assigning them rather than parsing anything.
    query        text,
    tags         text[]      not null default '{}',
    max_minutes  int,
    sort         text,

    created_by   uuid        not null references users (id) on delete restrict,
    created_at   timestamptz not null,
    updated_at   timestamptz not null
);

comment on table saved_searches is
    'A household-owned, named set of library filters. It records the question, never the answer: what matches is worked out whenever it is applied, so a recipe written this evening is in it immediately.';

comment on column saved_searches.query is
    'The words that were in the search box, or null. Held as typed, because search understands the words and this table must not have a second opinion about them.';

comment on column saved_searches.sort is
    'The order the search was read in, or null for whatever the library would have chosen. The same words GET /recipes accepts, which SearchOrders lists and an integration test holds the endpoint to.';

comment on column saved_searches.created_by is
    'on delete restrict, copying cookbooks.created_by: deleting an account must not silently delete a search the rest of the household still uses.';

-- A search that asks for nothing is the library, which is the screen it would
-- be applied from.
alter table saved_searches
    add constraint saved_searches_ask_something check (
        (query is not null and length(btrim(query)) > 0)
        or cardinality(tags) > 0
        or max_minutes is not null
        or sort is not null);

alter table saved_searches
    add constraint saved_searches_minutes_positive
        check (max_minutes is null or max_minutes > 0);

-- Two searches with one name in one kitchen are two things nobody can tell
-- apart in a row of chips. Case-insensitive, because "Quick dinners" and "quick
-- dinners" are the same name written twice.
create unique index saved_searches_named_once_idx
    on saved_searches (household_id, lower(name));

-- The only read: the household's searches, oldest first, so the row of chips
-- keeps the order they were made in and stops moving once they exist.
create index saved_searches_household_idx on saved_searches (household_id, created_at, id);
