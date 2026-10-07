#!/bin/sh
# Runs once on first container start, before PostgreSQL accepts connections: creates the application role.
# The app never connects as a superuser, so SQL injection can't install extensions, read other databases
# or write files.
#
# Names and password arrive as psql variables, quoted by psql (:"name" identifier, :'name' literal), never
# pasted by the shell. Variables aren't expanded inside a DO block's dollar quotes, so a query writes the
# statement and \gexec runs it.
set -eu

psql --username "$POSTGRES_USER" --dbname "$CULINA_DB" --no-psqlrc --set ON_ERROR_STOP=1 \
    --set db="$CULINA_DB" \
    --set role="$CULINA_APP_USER" \
    --set password="$CULINA_APP_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'role', :'password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'role')
\gexec

-- The app owns its schema so migrations can create tables, and has CREATE on
-- the database so the first migration can install the trusted extensions it
-- needs (citext, pg_trgm, unaccent). It is not a superuser and cannot touch
-- anything outside this database.
GRANT ALL ON DATABASE :"db" TO :"role";
ALTER SCHEMA public OWNER TO :"role";
SQL

echo "culina: application role '$CULINA_APP_USER' ready"
