-- The shopping list: one per household, created the first time anyone looks.
--
-- One list and not many. A second list is a planning feature, and planning is
-- not what a shopping list is for.

create table shopping_lists (
    id           uuid primary key,
    household_id uuid   not null unique references households (id) on delete cascade,
    version      bigint not null default 1
);

create table shopping_list_items (
    id          uuid primary key,
    list_id     uuid    not null references shopping_lists (id) on delete cascade,

    -- Both forms: the words the person wrote, and the folded form two lines are
    -- compared in, so the list shows their spelling and merges on ours.
    name        text    not null,
    name_key    text    not null,

    -- Stored unrounded. Rounding first and summing second compounds error, and
    -- three recipes each contributing a rounded 135 g produce a number nobody
    -- asked for. Rounding is presentation and belongs to the client.
    quantity    numeric(14, 4),
    unit        text,

    section     text    not null,
    is_checked  boolean not null default false,
    checked_at  timestamptz,
    sort_order  integer not null default 0,
    is_manual   boolean not null default false,

    constraint shopping_items_name_not_blank check (length(trim(name)) > 0),
    constraint shopping_items_checked_has_time
        check ((is_checked and checked_at is not null) or (not is_checked and checked_at is null))
);

-- The list is always read whole, in shop order.
create index shopping_list_items_by_list on shopping_list_items (list_id, section, sort_order);

-- Where a household has decided a thing actually lives.
--
-- A correction rather than a configuration screen: the seeded guess is right
-- most of the time, and the one time it is not, one tap fixes it for good.
create table shopping_section_overrides (
    household_id uuid not null references households (id) on delete cascade,
    name_key     text not null,
    section      text not null,

    primary key (household_id, name_key)
);
