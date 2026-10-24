-- Connecting an AI agent through OAuth 2.1, for MCP clients that cannot be handed a token (docs/api.md, "AI agents").
--
-- agent_clients holds apps that registered themselves (dynamic client registration, RFC 7591). Apps that use a
-- Client ID Metadata Document are not stored: their client_id is the document's address, read afresh each time.
-- Every app is a public client, so there is no secret to keep.
create table agent_clients (
    id            text        not null primary key check (length(id) between 1 and 100),
    name          text        not null check (length(name) between 1 and 80),
    redirect_uris text[]      not null check (cardinality(redirect_uris) between 1 and 10),
    created_at    timestamptz not null,
    -- When it last connected somebody; one that never does is forgotten after a day.
    last_used_at  timestamptz null
);

-- One app asking to connect someone's account: pending until a signed-in person answers on the consent screen,
-- then, if approved, an authorization code that is redeemed once for an agent session. Only the code's digest is
-- stored. expires_at is the answering deadline while pending and the code's deadline once approved. A row outlives
-- its deadline by a day so a code presented a second time can still end the session it became.
create table agent_authorizations (
    id                  uuid        not null primary key,
    client_id           text        not null check (length(client_id) between 1 and 2048),
    client_name         text        not null check (length(client_name) between 1 and 80),
    -- The host of the app's metadata document, which vouches for its name; null for a self-registered app.
    client_host         text        null,
    redirect_uri        text        not null check (length(redirect_uri) between 1 and 2048),
    code_challenge      text        not null check (length(code_challenge) = 43),
    state               text        null check (length(state) <= 2048),
    resource            text        not null,
    created_at          timestamptz not null,
    expires_at          timestamptz not null,
    user_id             uuid        null references users (id) on delete cascade,
    decided_at          timestamptz null,
    approved            boolean     not null default false,
    code_hash           bytea       null unique,
    redeemed_at         timestamptz null,
    session_id          uuid        null references sessions (id) on delete set null,
    approver_ip_address inet        null,
    approver_user_agent text        null,

    constraint agent_authorizations_code_when_approved check (approved = (code_hash is not null)),
    constraint agent_authorizations_decided_by_someone check (decided_at is null or user_id is not null)
);

create index agent_authorizations_expires_at on agent_authorizations (expires_at);

comment on table agent_authorizations is
    'OAuth authorization requests for the MCP server: pending, answered, and redeemed once for an agent session.';
