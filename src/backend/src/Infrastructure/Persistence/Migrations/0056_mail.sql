-- Mail (culina-v2-b6l6): the administrator can give Culina an SMTP account; the account settings live in the
-- settings table like every admin group. This adds what the mails need.

-- When the owner opened a link mailed to the address. Every account that exists already counts as confirmed: they
-- registered when nothing could have confirmed them, and turning mail on must not lock them out.
alter table users add column email_confirmed_at timestamptz;

update users set email_confirmed_at = created_at;

-- Single-use links mailed to an account's address: confirming the address, or signing in without a password.
-- Only the digest is stored; redeeming is one update requiring used_at to be null, so a link works once even when
-- opened twice at the same moment.
create table email_tokens (
    id         uuid        not null primary key,
    user_id    uuid        not null references users (id) on delete cascade,
    purpose    text        not null check (purpose in ('email_confirmation', 'sign_in')),
    token_hash bytea       not null unique,
    created_at timestamptz not null,
    expires_at timestamptz not null,
    used_at    timestamptz
);

create index email_tokens_user_id_idx on email_tokens (user_id);

-- Mail waiting to be sent, written in the transaction of whatever it is about and sent by a worker afterwards.
-- message is encrypted with the data protection key ring: a sign-in link waiting here is as good as a password.
-- A row is deleted once sent, or given up on after its last attempt.
create table mail_outbox (
    id         uuid        not null primary key,
    message    text        not null,
    created_at timestamptz not null,
    attempts   integer     not null default 0,
    retry_at   timestamptz not null
);

create index mail_outbox_retry_at_idx on mail_outbox (retry_at);

-- The kinds of device each person has signed in from (such as 'Firefox · macOS'), so a new one can be mailed to
-- them. The description is stored hashed: it is enough to recognise, and nobody needs to read it back.
create table known_devices (
    user_id       uuid        not null references users (id) on delete cascade,
    device_hash   bytea       not null,
    first_seen_at timestamptz not null,
    last_seen_at  timestamptz not null,
    primary key (user_id, device_hash)
);
