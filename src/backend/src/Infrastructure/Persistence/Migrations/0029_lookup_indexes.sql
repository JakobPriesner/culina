-- Indexes for lookups that scanned whole tables: the image reference check, the
-- suggestion scorer's per-household history, the intake worker's idle poll and
-- the foreign keys a recipe delete checks. No result changes.

-- "Is any recipe or cook-log entry still using this file?", per removed image.
create index recipe_images_content_hash_idx on recipe_images (content_hash);
create index cook_log_image_hash_idx on cook_log_entries (image_hash) where image_hash is not null;

-- The scorer reads one household's history; the existing indexes lead with recipe or person.
create index cook_log_household_idx on cook_log_entries (household_id, made_at desc);
create index cook_sessions_household_completed_idx on cook_sessions (household_id) where completed_at is not null;

-- The worker polls for due, undelivered notifications every couple of seconds.
create index recipe_intake_notifications_due_idx on recipe_intake_notifications (retry_at) where delivered_at is null;

-- Foreign keys the database checks when a recipe is deleted or purged.
create index meal_plan_entries_recipe_idx on meal_plan_entries (recipe_id);
create index shopping_list_item_sources_recipe_idx on shopping_list_item_sources (recipe_id);
create index suggestion_dismissals_recipe_idx on suggestion_dismissals (recipe_id);
create index recipe_intake_jobs_recipe_idx on recipe_intake_jobs (recipe_id) where recipe_id is not null;
create index recipe_intake_jobs_household_idx on recipe_intake_jobs (household_id);
