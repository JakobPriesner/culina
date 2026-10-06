#!/bin/sh
# Runs once, on first container start, before PostgreSQL accepts connections.
#
# Creates the application role. The app never connects as a superuser: a SQL
# injection that reached the database should not also be able to install
# untrusted extensions, read other databases, or write to the filesystem.
#
# The names and the password arrive as psql variables and are quoted by psql
# (:"name" as an identifier, :'name' as a literal), never pasted into the SQL by
# the shell — a password with a quote in it is a password, not a statement run
# as the superuser. Variables are not expanded inside a DO block's dollar
# quotes, so the role is created by a query that writes the statement and
# \gexec, which runs it.
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
