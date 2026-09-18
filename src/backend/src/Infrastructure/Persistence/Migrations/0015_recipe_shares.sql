-- A recipe published behind an unguessable link.
--
-- Everything else in Culina is read by somebody who is in the household. This
-- is the one thing a stranger can read, and it is deliberately the smallest
-- possible hole: one recipe, read only, at an address nobody can arrive at by
-- guessing, and gone the moment its row is deleted.

create table recipe_shares (
    -- The primary key, so a recipe has at most one link and the database is
    -- what says so. One link is the behaviour people already expect from every
    -- other app: pressing share twice hands you the same address, and the link
    -- you sent last month is still the link you sent last month.
    recipe_id  uuid        not null primary key references recipes (id) on delete cascade,

    -- Stored as written, not hashed, which is the one place this table departs
    -- from household_invitations. That code is hashed because it is used once
    -- and shown once; this link has to be readable again every time somebody
    -- asks "what was the address again". Hashing it would mean minting a new
    -- token on every look, which breaks the link already in somebody's chat.
    --
    -- The exposure a digest would buy is empty here: it protects against a
    -- database dump being replayed, and a dump of this database already
    -- contains the recipe this token reads.
    token      text        not null unique,

    created_by uuid        not null references users (id) on delete cascade,
    created_at timestamptz not null
);

comment on table recipe_shares is
    'One row per shared recipe. Its existence is the permission: revoking is deleting the row, and re-sharing afterwards mints a new token, so a link that was taken back stays dead.';

comment on column recipe_shares.token is
    'The whole credential. 256 bits of randomness from ISecretTokens, the same generator behind session cookies — an address nobody reaches without being sent it.';

comment on column recipe_shares.created_by is
    'on delete cascade, unlike cookbooks.created_by: publishing a recipe to the open web is one person''s act, and closing their account is the strongest way there is of taking it back.';
