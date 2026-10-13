-- Mealie, the second app a household can connect.
--
-- As recipe_sources.kind says it should be: a value here and a class in
-- Infrastructure, and nothing else.

alter table recipe_sources
    drop constraint recipe_sources_kind_known;

alter table recipe_sources
    add constraint recipe_sources_kind_known check (kind in ('tandoor', 'mealie'));

alter table recipe_origins
    drop constraint recipe_origins_kind_known;

alter table recipe_origins
    add constraint recipe_origins_kind_known check (kind in ('tandoor', 'mealie', 'web', 'ai'));
