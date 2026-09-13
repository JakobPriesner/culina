-- A photo of your own attempt, dated by the entry it hangs from.
--
-- Personal, like the note and the log itself: two people in one household keep
-- separate histories, and a picture of somebody's Tuesday dinner is theirs. The
-- recipe's own photograph is the household's and is unaffected.
--
-- Stored on the entry rather than in a table of its own, because unlike a
-- recipe's hero image there is nothing to replace: one attempt, one photo. The
-- bytes are content-addressed and live on the volume; only the hash is here.
alter table cook_log_entries
    add column image_hash   text,
    add column image_width  int,
    add column image_height int;

comment on column cook_log_entries.image_hash is
    'Content hash into the image store. A future sweep for unreferenced files must read this column as well as recipe_images.content_hash.';

alter table cook_log_entries
    add constraint cook_log_image_is_whole
        check (num_nonnulls(image_hash, image_width, image_height) in (0, 3));

-- The strip is read with the log, so no index of its own: the entries are
-- already fetched by (recipe_id, user_id, made_at desc).
