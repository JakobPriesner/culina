-- The extensions the schema is built on: citext (case-insensitive email
-- uniqueness), pg_trgm (fuzzy recipe search) and unaccent (diacritic folding).
--
-- All three are trusted extensions (PostgreSQL 13+), so the application role
-- installs them with nothing more than CREATE on its own database — no
-- superuser needed.
--
-- Numbered 0000 so a fresh database gets them before 0002 uses citext. An
-- instance created before this migration existed already has them, installed by
-- the old init script, and here they are left as they are.
create extension if not exists citext;
create extension if not exists pg_trgm;
create extension if not exists unaccent;
