-- Users, sessions, households and invitations.

create table users (
    id            uuid        not null primary key,
    -- citext so uniqueness is case-insensitive in the database rather than
    -- depending on every caller remembering to lowercase first.
    email         citext      not null unique,
    display_name  text        not null,
    password_hash text        not null,
    is_admin      boolean     not null default false,
    created_at    timestamptz not null,
    version       bigint      not null default 1
);

comment on column users.is_admin is
    'The first account created on an instance administers it; without that a fresh instance has no way in.';

create table user_settings (
    user_id            uuid   not null primary key references users (id) on delete cascade,
    locale             text   not null default 'en',
    theme              text   not null default 'warm-paper',
    mode               text   not null default 'system',
    measurement_system text   not null default 'metric',
    version            bigint not null default 1
);

create table sessions (
    id              uuid        not null primary key,
    user_id         uuid        not null references users (id) on delete cascade,
    -- The cookie carries 256 random bits; only its digest is stored, so a
    -- database dump cannot be replayed as a live session. A sequential id
    -- would be guessable, which is why the id is not the credential.
    token_hash      bytea       not null unique,
    -- The digest, never the token: a database dump must not let someone forge
    -- a CSRF header.
    csrf_token_hash bytea       not null,
    created_at      timestamptz not null,
    last_seen_at    timestamptz not null,
    expires_at      timestamptz not null,
    ip_address      inet,
    user_agent      text,
    revoked_at      timestamptz
);

create index sessions_user_id_idx on sessions (user_id);
create index sessions_expires_at_idx on sessions (expires_at);

create table households (
    id         uuid        not null primary key,
    name       text        not null,
    created_at timestamptz not null,
    version    bigint      not null default 1
);

create table household_members (
    household_id uuid        not null references households (id) on delete cascade,
    user_id      uuid        not null references users (id) on delete cascade,
    role         text        not null,
    joined_at    timestamptz not null,
    primary key (household_id, user_id)
);

create index household_members_user_id_idx on household_members (user_id);

create table household_invitations (
    id           uuid        not null primary key,
    household_id uuid        not null references households (id) on delete cascade,
    -- Hashed, because the code is a credential: anyone holding it can join.
    code_hash    bytea       not null unique,
    created_by   uuid        not null references users (id) on delete cascade,
    created_at   timestamptz not null,
    expires_at   timestamptz not null,
    redeemed_by  uuid        references users (id) on delete set null,
    redeemed_at  timestamptz
);

create index household_invitations_household_id_idx on household_invitations (household_id);
