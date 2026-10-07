-- Moves import photographs out of recipe_intake_jobs.material (base64, up to
-- 40 MB a job): listing jobs decompressed all of it on every poll. As rows of
-- their own, listing reads a small document and one photograph is one row.
-- Existing jobs are moved over, leaving an empty photos list.

create table recipe_intake_photos (
    job_id     uuid    not null references recipe_intake_jobs (id) on delete cascade,
    position   integer not null check (position >= 0),
    media_type text    not null,
    bytes      bytea   not null,
    primary key (job_id, position)
);

insert into recipe_intake_photos (job_id, position, media_type, bytes)
select j.id, (p.at - 1)::integer, p.photo ->> 'mediaType', decode(p.photo ->> 'bytes', 'base64')
from recipe_intake_jobs j
cross join lateral jsonb_array_elements(j.material -> 'photos') with ordinality as p(photo, at)
where jsonb_typeof(j.material -> 'photos') = 'array';

update recipe_intake_jobs
set material = jsonb_set(material, '{photos}', '[]'::jsonb)
where jsonb_typeof(material -> 'photos') = 'array' and jsonb_array_length(material -> 'photos') > 0;
