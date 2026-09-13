#!/usr/bin/env bash
#
# Proves the backup is a backup.
#
# Stands up a clean instance, puts real data in it, takes the three backups the
# runbook names, destroys everything a failed disk would destroy, restores, and
# then checks that what came back is what went in — counts, and a photograph
# that the person who uploaded it can still fetch and a stranger still cannot.
#
# An untested backup is a hope. This is the test, written down so it can be run
# again before a release rather than remembered as having gone well once.
#
#   scripts/restore-rehearsal.sh [image]
#
# Leaves nothing behind: its own compose project, its own volumes, torn down at
# the end whether it passed or failed.

set -euo pipefail

IMAGE="${1:-culina:dev}"
PROJECT="culina-rehearsal"
PORT="${CULINA_REHEARSAL_PORT:-18099}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
BASE="http://localhost:${PORT}"
PASSWORD="a sentence nobody else would pick"

export CULINA_IMAGE="$IMAGE"
export CULINA_PORT="$PORT"
export ForwardedHeaders__KnownNetworks="172.16.0.0/12"
export Database__Password="rehearsal_password"

compose() {
  docker compose -p "$PROJECT" -f "$ROOT/compose.yaml" -f "$ROOT/compose.prod.yaml" "$@"
}

cleanup() {
  compose down -v >/dev/null 2>&1 || true
  rm -rf "$WORK"
}

trap cleanup EXIT

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }

wait_for_ready() {
  local deadline=$((SECONDS + 120))

  until [ "$(curl -s -o /dev/null -w '%{http_code}' "${BASE}/health/ready")" = "200" ]; do
    [ "$SECONDS" -lt "$deadline" ] || { echo "never became ready"; exit 1; }
    sleep 2
  done
}

api() { curl -sS -b "$WORK/jar" -c "$WORK/jar" "$@"; }
csrf() { awk '/culina.csrf/ { print $7 }' "$WORK/jar"; }

# ── A clean instance ─────────────────────────────────────────────────────────
say "Starting a clean instance on ${BASE}"
compose down -v >/dev/null 2>&1 || true
compose up -d >/dev/null
wait_for_ready

# ── Something worth losing ───────────────────────────────────────────────────
say "Putting data in"
api -X POST "${BASE}/api/v1/users" -H 'Content-Type: application/json' -H "Origin: ${BASE}" \
  -d "{\"email\":\"owner@culina.test\",\"displayName\":\"Owner\",\"password\":\"${PASSWORD}\",\"householdName\":\"Rehearsal kitchen\"}" \
  -o /dev/null
api -X POST "${BASE}/api/v1/sessions" -H 'Content-Type: application/json' -H "Origin: ${BASE}" \
  -d "{\"email\":\"owner@culina.test\",\"password\":\"${PASSWORD}\"}" -o /dev/null

TOKEN="$(csrf)"
HOUSEHOLD="$(api "${BASE}/api/v1/users/me" | python3 -c 'import sys,json; print(json.load(sys.stdin)["households"][0]["householdId"])')"

for title in "Tarte Tatin" "Zitronen-Orzo" "Bolognese"; do
  api -X POST "${BASE}/api/v1/recipes" -H 'Content-Type: application/json' \
    -H "X-Culina-CSRF: ${TOKEN}" -H "Origin: ${BASE}" \
    -d "{\"householdId\":\"${HOUSEHOLD}\",\"title\":\"${title}\"}" -o /dev/null
done

RECIPE="$(api "${BASE}/api/v1/recipes?householdId=${HOUSEHOLD}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["items"][0]["recipeId"])')"

UPLOADED="$(api -X PUT "${BASE}/api/v1/recipes/${RECIPE}/image" \
  -H "X-Culina-CSRF: ${TOKEN}" -H "Origin: ${BASE}" \
  -F "file=@${ROOT}/src/frontend/static/images/culina-orzo.webp" \
  -o /dev/null -w '%{http_code}')"

[ "$UPLOADED" = "200" ] || { echo "the photograph did not upload: ${UPLOADED}"; exit 1; }

RECIPES_BEFORE="$(api "${BASE}/api/v1/recipes?householdId=${HOUSEHOLD}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["total"])')"
MEMBERS_BEFORE="$(api "${BASE}/api/v1/households/${HOUSEHOLD}/members" | python3 -c 'import sys,json; print(len(json.load(sys.stdin)["items"]))')"

echo "  ${RECIPES_BEFORE} recipes, ${MEMBERS_BEFORE} member(s), one photograph"

# ── The backup the runbook names ─────────────────────────────────────────────
say "Backing up"
compose exec -T db pg_dump -U postgres --format=custom culina > "$WORK/culina.dump"
docker run --rm -v "${PROJECT}_culina-images:/data" -v "$WORK:/backup" alpine \
  tar czf /backup/images.tar.gz -C /data . >/dev/null
docker run --rm -v "${PROJECT}_culina-keys:/data" -v "$WORK:/backup" alpine \
  tar czf /backup/keys.tar.gz -C /data . >/dev/null

echo "  $(du -h "$WORK/culina.dump" | cut -f1) database, $(du -h "$WORK/images.tar.gz" | cut -f1) images"

# ── The disaster ─────────────────────────────────────────────────────────────
say "Destroying everything"
compose down -v >/dev/null

# ── The restore, timed ───────────────────────────────────────────────────────
say "Restoring"
STARTED_AT="$SECONDS"

compose up -d db >/dev/null
until compose exec -T db pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done

compose exec -T db psql -U postgres -d postgres -c "drop database if exists culina;" >/dev/null
compose exec -T db psql -U postgres -d postgres -c "create database culina owner culina_app;" >/dev/null
compose exec -T db pg_restore -U postgres -d culina --no-owner --role=culina_app < "$WORK/culina.dump" >/dev/null

docker run --rm -v "${PROJECT}_culina-images:/data" -v "$WORK:/backup" alpine \
  tar xzf /backup/images.tar.gz -C /data >/dev/null
docker run --rm -v "${PROJECT}_culina-keys:/data" -v "$WORK:/backup" alpine \
  tar xzf /backup/keys.tar.gz -C /data >/dev/null

compose up -d >/dev/null
wait_for_ready

RECOVERY_SECONDS=$((SECONDS - STARTED_AT))

# ── What came back ───────────────────────────────────────────────────────────
say "Checking what came back"
rm -f "$WORK/jar"

api -X POST "${BASE}/api/v1/sessions" -H 'Content-Type: application/json' -H "Origin: ${BASE}" \
  -d "{\"email\":\"owner@culina.test\",\"password\":\"${PASSWORD}\"}" -o /dev/null

RECIPES_AFTER="$(api "${BASE}/api/v1/recipes?householdId=${HOUSEHOLD}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["total"])')"
MEMBERS_AFTER="$(api "${BASE}/api/v1/households/${HOUSEHOLD}/members" | python3 -c 'import sys,json; print(len(json.load(sys.stdin)["items"]))')"
IMAGE_STATUS="$(api -o /dev/null -w '%{http_code}' "${BASE}/api/v1/recipes/${RECIPE}/image?w=800")"
STRANGER_STATUS="$(curl -sS -o /dev/null -w '%{http_code}' "${BASE}/api/v1/recipes/${RECIPE}/image?w=800")"

failed=0
check() {
  if [ "$2" = "$3" ]; then
    printf '  \033[32m✓\033[0m %s\n' "$1"
  else
    printf '  \033[31m✗\033[0m %s — expected %s, got %s\n' "$1" "$3" "$2"
    failed=1
  fi
}

check "the password still signs in"            "$(api -o /dev/null -w '%{http_code}' "${BASE}/api/v1/users/me")" "200"
check "every recipe is back"                   "$RECIPES_AFTER"   "$RECIPES_BEFORE"
check "every member is back"                   "$MEMBERS_AFTER"   "$MEMBERS_BEFORE"
check "the photograph is served to its owner"  "$IMAGE_STATUS"    "200"
check "and still refused to a stranger"        "$STRANGER_STATUS" "401"

say "Recovered in ${RECOVERY_SECONDS}s"

exit "$failed"
