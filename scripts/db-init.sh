#!/bin/sh
# Runs once, on first container start, before PostgreSQL accepts connections.
#
# Creates the application role. The app never connects as a superuser: a SQL
# injection that reached the database should not also be able to install
# untrusted extensions, read other databases, or write to the filesystem.
set -eu

psql --username "$POSTGRES_USER" --dbname "$CULINA_DB" --no-psqlrc --set ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$CULINA_APP_USER') THEN
        CREATE ROLE $CULINA_APP_USER LOGIN PASSWORD '$CULINA_APP_PASSWORD';
    END IF;
END
\$\$;

-- The app owns its schema so migrations can create tables, and has CREATE on
-- the database so the first migration can install the trusted extensions it
-- needs (citext, pg_trgm, unaccent). It is not a superuser and cannot touch
-- anything outside this database.
GRANT ALL ON DATABASE $CULINA_DB TO $CULINA_APP_USER;
ALTER SCHEMA public OWNER TO $CULINA_APP_USER;
SQL

echo "culina: application role '$CULINA_APP_USER' ready"
