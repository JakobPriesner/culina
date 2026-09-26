-- The household this one inherits recipes from.
--
-- A household that inherits another sees every recipe that one can see — its
-- own, and whatever it inherits in turn — and edits none of them: they stay
-- the other kitchen's. One parent, not several, so "where does this recipe
-- come from" always has one answer and a library is a chain rather than a
-- graph somebody has to be shown.
--
-- on delete set null: deleting the parent takes its recipes with it, and the
-- heir simply stops inheriting rather than going with them. Cycles cannot be
-- expressed in a check constraint and are refused by the application; the
-- recursive reads stop at a repeated household, so even one that slipped past
-- would end.
alter table households
    add column inherits_from uuid references households (id) on delete set null,
    add constraint households_inherits_from_not_self check (inherits_from <> id);

-- "Which households inherit from this one" is how a recipe read finds out
-- whether an heir's member may see it. Partial, because most households
-- inherit nothing.
create index households_inherits_from_idx
    on households (inherits_from)
    where inherits_from is not null;

-- Every household whose recipes this one sees: itself, then what it inherits,
-- however far up. For SQL that meets a household in a row rather than in a
-- parameter — a cookbook's shelf is a household's library, and a list of
-- shelves is several rows at once. Where the household is a parameter the
-- application passes the same list as an array instead, which the planner can
-- see into. union, not union all, so a repeated household ends the walk.
create function household_library(household uuid) returns setof uuid
language sql stable
rows 4
as $$
    with recursive chain (id) as (
        select household
        union
        select h.inherits_from
        from households h
        join chain on h.id = chain.id
        where h.inherits_from is not null
    )
    select id from chain;
$$;
