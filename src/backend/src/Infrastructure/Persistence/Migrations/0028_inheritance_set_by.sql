-- Who set each household's inheritance, so it ends when they leave the parent.
--
-- Inheriting is set from the heir's side, by an owner of the heir who is in
-- the parent: the parent's recipes are theirs to show. Nothing remembered who
-- that was, so somebody shown out of the parent — or who left it — kept every
-- recipe of it through a household of their own, and so did everybody they
-- had invited there.
--
-- inherits_set_by is that person, and the pair is now a foreign key to their
-- membership of the parent. When the membership row goes — removed, leaving,
-- the account deleted, the parent purged — the database clears the link in the
-- same statement, so no code path can forget to. match full: a link is both
-- columns or neither. It replaces the plain foreign key on inherits_from, which
-- the membership row implies. A chain needs nothing more: every link answers
-- to its own parent's members.
--
-- The application writes memberships as changes rather than delete-everybody-
-- and-insert-again, which would cut every heir loose on every save.

alter table households_with_deleted add column inherits_set_by uuid;

-- Existing links are credited to an owner of the heir who is in the parent:
-- somebody who could have set it today.
update households_with_deleted h
set inherits_set_by = (
    select owner.user_id
    from household_members owner
    join household_members parent
        on parent.household_id = h.inherits_from and parent.user_id = owner.user_id
    where owner.household_id = h.id and owner.role = 'owner'
    order by owner.joined_at, owner.user_id
    limit 1)
where h.inherits_from is not null;

-- Where nobody like that is left, the link is one somebody's departure should
-- already have closed.
update households_with_deleted
set inherits_from = null, version = version + 1
where inherits_from is not null and inherits_set_by is null;

alter table households_with_deleted
    drop constraint households_inherits_from_fkey,
    add constraint households_inherits_from_member_fkey
        foreign key (inherits_from, inherits_set_by)
        references household_members (household_id, user_id)
        match full
        on delete set null;

-- A view's column list is fixed when it is created (0025), so households is
-- created again to show the new column, and recipes and cookbooks with it
-- because they are defined over it.
drop view cookbooks;
drop view recipes;
drop view households;

create view households as
select * from households_with_deleted
where deleted_at is null;

create view recipes as
select * from recipes_with_deleted r
where r.deleted_at is null
  and exists (select 1 from households h where h.id = r.household_id);

create view cookbooks as
select * from cookbooks_with_deleted c
where c.deleted_at is null
  and exists (select 1 from households h where h.id = c.household_id);
