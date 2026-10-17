-- Whether a recipe's picture was drawn by the assistant rather than photographed or
-- imported, so the editor can say so beside that picture and only beside that one.
-- Existing pictures predate the distinction and stay unmarked.
alter table recipe_images add column drawn boolean not null default false;
