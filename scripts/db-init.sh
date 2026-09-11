#!/bin/sh
# Runs once, on first container start, before PostgreSQL accepts connections.
#
# Creates the application role. The app never connects as a superuser: a SQL
# injection that reached the database should not also be able to create
# extensions, read other databases, or write to the filesystem.
set -eu

psql --username "$POSTGRES_USER" --dbname "$CULINA_DB" --no-psqlrc --set ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$CULINA_APP_USER') THEN
        CREATE ROLE $CULINA_APP_USER LOGIN PASSWORD '$CULINA_APP_PASSWORD';
    END IF;
END
\$\$;

-- The app owns its schema so migrations can create tables, but it is not a
-- superuser and cannot touch anything outside this database.
GRANT ALL ON DATABASE $CULINA_DB TO $CULINA_APP_USER;
ALTER SCHEMA public OWNER TO $CULINA_APP_USER;

-- citext (case-insensitive email uniqueness) and pg_trgm (recipe search) need
-- superuser rights to install, so they are created here rather than in a
-- migration the application role would fail to run.
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;
SQL

echo "culina: application role '$CULINA_APP_USER' and extensions ready"
