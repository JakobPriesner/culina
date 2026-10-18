-- A planned meal can be the leftovers of another: the batch cooked on Monday,
-- eaten again on Tuesday. The original is planned (and shopped) for the whole
-- batch; the leftover entry asks nothing of the shopping list. Taking the
-- original off the plan takes its leftovers with it.
alter table meal_plan_entries
    add column leftover_of uuid references meal_plan_entries (id) on delete cascade;
