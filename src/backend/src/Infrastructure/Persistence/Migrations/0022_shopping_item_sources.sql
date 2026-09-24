-- What each shopping-list line is a sum of.
--
-- A line merges every recipe that asks for the same thing, so 300 g of butter
-- may be 200 g for one meal and 100 g for another. Without its sources the
-- list could not tell a planned meal it already holds from one it does not —
-- adding a week twice doubled it — and could not take one meal back off
-- without guessing how much of the 300 g was that meal's.
--
-- The quantity is the recipe's, scaled and in the unit it was written in; the
-- line keeps the sum. plan_entry_id carries no foreign key on purpose: a meal
-- taken off the plan can still have its shopping taken off the list after it.
--
-- Lines written before this have no sources and stay exactly as they are.
create table shopping_list_item_sources (
    item_id       uuid           not null references shopping_list_items (id) on delete cascade,
    recipe_id     uuid           not null references recipes (id) on delete cascade,
    plan_entry_id uuid,
    quantity      numeric(14, 4),
    unit          text
);

create index shopping_list_item_sources_item_idx on shopping_list_item_sources (item_id);

create index shopping_list_item_sources_plan_entry_idx
    on shopping_list_item_sources (plan_entry_id)
    where plan_entry_id is not null;
