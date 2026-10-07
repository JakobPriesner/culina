#!/usr/bin/env bash
#
# Does the setup screen's first step unattended: points a fresh instance from compose.prod.yaml at its
# database (the sign-in is the setup screen's to ask, so unattended starts must answer it) and waits
# for the restart into the real app.
#
#   Database__Password=… scripts/setup-database.sh http://localhost:8080
#
# Role and password are those the db service created from the same variables. Instances past this step are left alone.

set -euo pipefail

BASE="${1:?usage: scripts/setup-database.sh <base url>}"
: "${Database__Password:?set Database__Password, the password the db service gave the app role}"

# "<stage> <startedAt>", or nothing while the server is between hosts.
setup() {
  curl -sS --fail "${BASE}/api/v1/setup" 2>/dev/null \
    | python3 -c 'import sys,json; s=json.load(sys.stdin); print(s["stage"], s["startedAt"])' 2>/dev/null \
    || true
}

BEFORE="$(setup)"

[ "${BEFORE%% *}" = "database" ] || exit 0

# Sent on stdin so the password is never an argument visible in a process listing.
STATUS="$(python3 -c 'import json,os; print(json.dumps({
    "host": "db",
    "port": 5432,
    "name": os.environ.get("POSTGRES_DB") or "culina",
    "username": os.environ.get("Database__Username") or "culina_app",
    "password": os.environ["Database__Password"],
    "requireSsl": False,
    "maxPoolSize": 20,
}))' | curl -sS -o /dev/stderr -w '%{http_code}' -X PUT "${BASE}/api/v1/settings/database" \
  -H 'Content-Type: application/json' -H "Origin: ${BASE}" --data-binary @-)"

[ "$STATUS" = "202" ] || { echo "the database was not saved: ${STATUS}" >&2; exit 1; }

# 202 means saved and restarting; done when a new host answers (changed startedAt) and no longer asks for a database.
DEADLINE=$((SECONDS + 60))

until NOW="$(setup)"; [ -n "$NOW" ] && [ "$NOW" != "$BEFORE" ] && [ "${NOW%% *}" != "database" ]; do
  [ "$SECONDS" -lt "$DEADLINE" ] || { echo "the server never came back from setting up its database" >&2; exit 1; }
  sleep 1
done
