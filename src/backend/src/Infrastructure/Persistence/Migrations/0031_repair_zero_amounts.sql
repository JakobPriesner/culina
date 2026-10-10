-- Repairs amounts and yields that were stored as zero.
--
-- Before culina-v2-ozv7 an amount like 0.0004 passed validation and was
-- rounded to 0.000 by its column. Reading such a row is refused (the domain
-- only holds amounts that are positive after rounding to three places), so the
-- recipe or shopping list answered 500 on every read.
--
-- An amount that is no amount becomes an unmeasured one (no amount, no unit —
-- a unit without an amount is dropped on write too); a yield becomes 1, the
-- default.

update recipe_ingredients
set quantity = null, unit = null
where quantity is not null and round(quantity, 3) <= 0;

update recipes_with_deleted
set yield_amount = 1
where round(yield_amount, 3) <= 0;

update shopping_list_items
set quantity = null, unit = null
where quantity is not null and round(quantity, 3) <= 0;

update shopping_list_item_sources
set quantity = null, unit = null
where quantity is not null and round(quantity, 3) <= 0;
