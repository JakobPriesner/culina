-- Connected-source tokens are encrypted from now on, with the same
-- data-protection key ring as the assistant's API key, so a database dump no
-- longer hands out working credentials for somebody's Tandoor.
--
-- SQL cannot do the encrypting — the keys are not in the database, which is the
-- point — so this only records which rows still hold a plain token. Every row
-- that existed before this migration does. The app encrypts those at startup,
-- before it serves a request, and writes every new row encrypted.
alter table recipe_sources
    add column secret_protected boolean not null default false;

comment on column recipe_sources.secret_protected is
    'Whether secret holds the token encrypted with the data-protection key ring (true) or a plain token stored before encryption existed (false). Plain rows are encrypted at startup.';
