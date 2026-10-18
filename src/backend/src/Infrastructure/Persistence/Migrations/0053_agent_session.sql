-- An AI agent using the MCP server at /api/mcp.
--
-- It signs in as client 'agent' and keeps the bearer token sign-in hands out.
-- The token reaches the MCP endpoint and the operations it offers as tools,
-- nothing else; its owner revokes it like any other session.

alter table sessions
    drop constraint sessions_client_check;

alter table sessions
    add constraint sessions_client_check check (client in ('browser', 'ios', 'android', 'instance', 'agent'));

comment on column sessions.client is
    'browser (cookie), ios or android (bearer token), instance: another Culina importing recipes (bearer token), or agent: an AI agent using the MCP server (bearer token, MCP only).';
