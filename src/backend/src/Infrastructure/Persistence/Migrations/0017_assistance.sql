-- What the assistant was asked for, and what it cost.
--
-- The one feature in Culina that spends money per use, which is what this table
-- is for. Everything else here either works or does not; an API key behind a
-- web interface can also work far more often than anybody meant it to, and the
-- only defence against that is counting.
--
-- A row per call rather than a running total per month, because the two
-- questions people actually ask — "why was the bill that size" and "who is
-- using this" — are both answered by the detail and neither by the total. Three
-- people cooking is a few hundred rows a month.
create table assistance_usage (
    id            uuid        not null primary key,

    -- Who asked. Cascades: somebody who deletes their account should not leave
    -- their spending behind with their name still attached to it. The month's
    -- total shrinks by their share, which is the honest answer — the money is
    -- spent either way, but this table is about people rather than accounting.
    user_id       uuid        not null references users (id) on delete cascade,
    -- Whose kitchen it was for. Null once that household is gone, and null from
    -- the start for a call that was not about any particular one.
    household_id  uuid                 references households (id) on delete set null,

    capability    text        not null,
    provider      text        not null,
    model         text        not null,

    input_tokens  int         not null default 0,
    output_tokens int         not null default 0,
    pictures      int         not null default 0,

    -- What it might cost, written before the call. The budget gate needs a
    -- number at the moment it decides, and the real one does not exist yet.
    estimate      numeric(12, 6) not null,
    -- What it did cost, written after. Null means this app has no price for
    -- that model: a number nobody can check against an invoice is worse than an
    -- empty cell, so an unpriced model records its tokens and says nothing
    -- about money.
    cost          numeric(12, 6),

    -- 'reserved' until the call comes back, then 'ok' or the error code. A row
    -- left at 'reserved' is a process that stopped mid-call; it holds its
    -- estimate against the budget until the month turns, which is the safe
    -- direction to be wrong in.
    outcome       text        not null,

    occurred_at   timestamptz not null,
    -- The correlation id of the request that caused it, so a line on the
    -- settings screen can be found in the logs.
    request_id    text
);

comment on table assistance_usage is
    'One row per call to a model provider. Instance-wide: the assistant is connected once by an administrator, so this is not household-owned even though most of what it is asked about is.';

comment on column assistance_usage.estimate is
    'Reserved against the budget before the call, and still what counts while outcome is ''reserved''. Deliberately generous rather than accurate — its only job is to stop two simultaneous requests both fitting into the last of the money.';

comment on column assistance_usage.cost is
    'Null for a model this app has no price for. The settings screen counts those calls separately rather than folding a guess into the total.';

-- The only two reads: this month across the instance, and this month for one
-- person. Both are the budget gate as well as the settings screen, so they are
-- on the hot path of every assisted request.
create index assistance_usage_period_idx on assistance_usage (occurred_at);
create index assistance_usage_person_idx on assistance_usage (user_id, occurred_at);

-- A recipe the assistant drafted knows that it did.
--
-- The same table an imported recipe uses, and for the same reason: where a
-- recipe came from is one fact with one shape, and a second table for a second
-- kind of elsewhere would be two places to ask the same question. A drafted
-- recipe is an ordinary recipe — edited, cooked, scaled and planned exactly
-- like one somebody typed — and the only thing that distinguishes it is a line
-- saying where it started.
--
-- external_id is the draft's own id, so the uniqueness rule below stays
-- meaningful: every draft is its own, and asking twice makes two.
alter table recipe_origins
    drop constraint recipe_origins_kind_known;

alter table recipe_origins
    add constraint recipe_origins_kind_known check (kind in ('tandoor', 'web', 'ai'));
