-- Indexes for lookups that were scanning whole tables.
--
-- Every one of these is a read that filters or joins on a column nothing
-- indexed: the image reference check on every removal and on the trash purge,
-- the suggestion scorer's per-household history, the intake worker's idle poll,
-- and the foreign keys a recipe delete has to check before it can cascade.
-- None of them changes a result, only how many rows are looked at to find it.

-- "Is any recipe or cook-log entry still using this file?" runs for every image
-- that is removed or replaced, and for every candidate of the trash purge.
create index recipe_images_content_hash_idx on recipe_images (content_hash);
create index cook_log_image_hash_idx on cook_log_entries (image_hash) where image_hash is not null;

-- The suggestion scorer reads one household's history in five places. The
-- existing indexes lead with the recipe or the person, so each read walked
-- every household's entries.
create index cook_log_household_idx on cook_log_entries (household_id, made_at desc);
create index cook_sessions_household_completed_idx on cook_sessions (household_id) where completed_at is not null;

-- The worker looks for notifications that are due and not yet delivered, every
-- couple of seconds, and the table only grows.
create index recipe_intake_notifications_due_idx on recipe_intake_notifications (retry_at) where delivered_at is null;

-- Foreign keys the database checks when a recipe is deleted or purged.
create index meal_plan_entries_recipe_idx on meal_plan_entries (recipe_id);
create index shopping_list_item_sources_recipe_idx on shopping_list_item_sources (recipe_id);
create index suggestion_dismissals_recipe_idx on suggestion_dismissals (recipe_id);
create index recipe_intake_jobs_recipe_idx on recipe_intake_jobs (recipe_id) where recipe_id is not null;
create index recipe_intake_jobs_household_idx on recipe_intake_jobs (household_id);
