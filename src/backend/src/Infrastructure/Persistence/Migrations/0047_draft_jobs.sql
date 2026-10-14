-- The composer's drafts become server jobs (docs/native-app-design.md, part 8).
--
-- A draft asked for in the app kept going only while the page held its stream
-- open; a phone that put the app away cut it. As a job it is written by the
-- intake worker whatever the client does, and the page re-attaches through
-- the same watch stream imports use. A draft job ends at 'ready' with the
-- draft and no recipe: the person corrects it and saves it themselves.
--
-- What kind of draft, and for a revision which recipe, travel in `material`
-- (`draftKind`, `recipeId`), beside what the person wrote.
alter table recipe_intake_jobs
    add column purpose text not null default 'import' check (purpose in ('import', 'draft'));
