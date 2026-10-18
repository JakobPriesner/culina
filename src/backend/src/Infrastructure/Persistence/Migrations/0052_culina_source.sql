-- Another Culina, the third app a household can connect.
--
-- It signs in to the other instance as client 'instance' and keeps the
-- bearer token that sign-in hands out, so the other instance's sessions say
-- what the session is for and its owner can revoke it like any other.

alter table recipe_sources
    drop constraint recipe_sources_kind_known;

alter table recipe_sources
    add constraint recipe_sources_kind_known check (kind in ('tandoor', 'mealie', 'culina'));

alter table recipe_origins
    drop constraint recipe_origins_kind_known;

alter table recipe_origins
    add constraint recipe_origins_kind_known check (kind in ('tandoor', 'mealie', 'culina', 'web', 'ai'));

alter table sessions
    drop constraint sessions_client_check;

alter table sessions
    add constraint sessions_client_check check (client in ('browser', 'ios', 'android', 'instance'));

comment on column sessions.client is
    'browser (cookie), ios or android (bearer token), or instance: another Culina importing recipes (bearer token).';
