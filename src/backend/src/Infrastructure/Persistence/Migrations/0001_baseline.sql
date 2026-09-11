-- Culina baseline schema.
--
-- Forward-only: this file is never edited once it has run anywhere. A change
-- is a new migration, and the runner refuses to start if an applied
-- migration's checksum no longer matches.

-- Instance settings an admin edits from the app's own screens: one JSONB row
-- per group rather than a column per value, so adding a checkbox is not a
-- schema migration.
create table settings (
    group_name text        not null primary key,
    payload    jsonb       not null,
    updated_at timestamptz not null default now()
);

comment on table settings is
    'Admin-editable instance settings, one row per settings group.';
