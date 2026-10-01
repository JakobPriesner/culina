-- One-time codes that set a new password for an account that lost its own.
--
-- Culina has no mail transport, so recovery is something a person holds rather
-- than something sent to them: either one of the ten codes they saved while they
-- still knew their password (issued_by null, no expiry), or a code the instance
-- administrator issued for them (issued_by set, expires within a day).
--
-- Hashed, because the code is a credential: anyone holding it, with the
-- address, can take the account. used_at makes it single use; redeeming is one
-- update that requires it to be null, so two concurrent redemptions cannot
-- both succeed.
create table recovery_codes (
    id         uuid        not null primary key,
    user_id    uuid        not null references users (id) on delete cascade,
    code_hash  bytea       not null unique,
    issued_by  uuid        references users (id) on delete set null,
    created_at timestamptz not null,
    expires_at timestamptz,
    used_at    timestamptz
);

create index recovery_codes_user_id_idx on recovery_codes (user_id);
