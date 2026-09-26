#!/usr/bin/env bash
#
# What CI holds main to, run here first.
#
# The Backend, Frontend, Contract and End to end jobs of
# .github/workflows/ci.yml, step for step, against one commit. The pre-push hook
# (.beads/hooks/pre-push) runs it for anything on its way to main, because a red
# main is found out by everybody and a red push only by whoever made it.
#
#   scripts/ci-local.sh [commit]        # HEAD when left out; also `make ci`
#
# The commit is checked out into a worktree of its own, so what is tested is
# what is pushed and not whatever is lying uncommitted beside it, and nothing
# in this checkout is touched. The worktree, the databases and the stack are
# torn down at the end whether it passed or failed; the logs of a failure stay.
#
# Needs Docker: the contract export, the integration tests and the end-to-end
# stack each start a PostgreSQL of their own. CodeQL and the security scans are
# left to CI.

set -euo pipefail

command -v docker >/dev/null && docker info >/dev/null 2>&1 ||
  { echo "ci-local: Docker is not running, and every database here is a container." >&2; exit 1; }

# Run as CI, Playwright starts its own preview on this port rather than trusting
# one that is already there, which may be serving an older build.
if lsof -nP -iTCP:4173 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "ci-local: port 4173 is taken, most likely by a vite preview left running. Stop it and try again." >&2
  exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMMIT="$(git -C "$ROOT" rev-parse --verify "${1:-HEAD}^{commit}")"
SHORT="${COMMIT:0:7}"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/culina-ci.XXXXXX")"
TREE="$WORK/tree"
LOGS="$WORK/logs"
BACKEND="$TREE/src/backend"
FRONTEND="$TREE/src/frontend"
PROJECT="culina-ci-local"
CONTRACT_DB="culina-ci-local-contract"
PORT="$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1])')"
BASE="http://localhost:${PORT}"
# The account CI signs the suites in with, on an instance that exists for it.
EMAIL="ci@culina.test"
PASSWORD="a sentence nobody else would pick"
LANES=""

# The stack's settings go to compose alone. Exported, the raised rate limits
# would reach the integration tests, and those assert the real ones.
compose() {
  CULINA_IMAGE="culina:ci-local" \
    CULINA_PORT="$PORT" \
    Database__Password="culina_ci_password" \
    RateLimits__LoginPerIpPerMinute=1000 \
    RateLimits__RegisterPerIpPerHour=1000 \
    RateLimits__RequestsPerSessionPerMinute=20000 \
    ForwardedHeaders__KnownNetworks="172.16.0.0/12" \
    docker compose -p "$PROJECT" -f "$TREE/compose.yaml" -f "$TREE/compose.prod.yaml" "$@"
}

# A lane is a subshell running a subshell running dotnet or docker, and without
# job control none of them hears Ctrl+C. Stopping only the first would leave
# the rest building into a deleted worktree, or starting the stack after it was
# torn down.
stop() {
  local child

  for child in $(pgrep -P "$1" 2>/dev/null); do
    stop "$child"
  done

  kill "$1" 2>/dev/null || true
}

cleanup() {
  local status=$? lane

  for lane in $LANES; do
    stop "$lane"
  done

  if [ "$status" -ne 0 ] && [ -d "$FRONTEND/playwright-report" ]; then
    cp -R "$FRONTEND/playwright-report" "$FRONTEND/test-results" "$LOGS/" 2>/dev/null || true
  fi

  compose down -v --remove-orphans >/dev/null 2>&1 || true
  docker rm -f "$CONTRACT_DB" >/dev/null 2>&1 || true
  git -C "$ROOT" worktree remove --force "$TREE" >/dev/null 2>&1 || true

  if [ "$status" -eq 0 ]; then
    rm -rf "$WORK"
  else
    printf '\n\033[31mci-local: %s would fail CI.\033[0m Logs: %s\n' "$SHORT" "$LOGS" >&2
  fi
}

# One line per step. The output goes to a log, which is shown only on failure.
step() {
  local name="$1" dir="$2"
  shift 2

  if (cd "$dir" && "$@") >"$LOGS/$name.log" 2>&1; then
    printf '  \033[32m✓\033[0m %s\n' "$name"
  else
    printf '  \033[31m✗\033[0m %s\n' "$name"
    tail -n 40 "$LOGS/$name.log" | sed 's/^/      /'
    return 1
  fi
}

# The export needs a started host, and so a database; CI gives it a bare one.
# Written beside the tree rather than into it, so the frontend steps running
# at the same time never read a client halfway through being rewritten.
contract() {
  docker run -d --rm --name "$CONTRACT_DB" -p 127.0.0.1::5432 \
    -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=culina postgres:18-alpine || return 1

  # Over TCP, which the entrypoint's first, initialising start does not listen on.
  local deadline=$((SECONDS + 60))
  until docker exec "$CONTRACT_DB" pg_isready -h 127.0.0.1 -U postgres; do
    [ "$SECONDS" -lt "$deadline" ] || return 1
    sleep 1
  done

  Database__Host=127.0.0.1 \
    Database__Port="$(docker port "$CONTRACT_DB" 5432/tcp | head -n 1 | sed 's/.*://')" \
    Database__Name=culina \
    Database__Username=postgres \
    Database__Password=postgres \
    Database__RequireSsl=false \
    dotnet run --no-build --project src/Api -- --export-openapi "$WORK/Api.json" || return 1

  (cd "$FRONTEND" && pnpm exec openapi-typescript "$WORK/Api.json" --output "$WORK/schema.d.ts") ||
    return 1

  diff -u openapi/Api.json "$WORK/Api.json" &&
    diff -u "$FRONTEND/src/lib/api/generated/schema.d.ts" "$WORK/schema.d.ts" ||
    { echo "The OpenAPI document or the generated client is stale. Run 'make api' and commit the result."; return 1; }
}

first_account() {
  curl --fail --silent --show-error -X POST "${BASE}/api/v1/users" \
    -H 'Content-Type: application/json' -H "Origin: ${BASE}" \
    -d "{\"email\":\"${EMAIL}\",\"displayName\":\"CI\",\"password\":\"${PASSWORD}\"}"
}

mkdir "$LOGS"
trap cleanup EXIT

printf '\033[1mci-local: %s %s\033[0m\n' "$SHORT" "$(git -C "$ROOT" log -1 --format=%s "$COMMIT")"
git -C "$ROOT" worktree add --detach --quiet "$TREE" "$COMMIT"
# Not the leftovers of an earlier run that was killed before it could clean up.
compose down -v --remove-orphans >/dev/null 2>&1 || true
docker rm -f "$CONTRACT_DB" >/dev/null 2>&1 || true

step "dotnet restore" "$BACKEND" dotnet restore --locked-mode
step "pnpm install" "$FRONTEND" pnpm install --frozen-lockfile

# Three lanes at once, each stopping at its first failure. A failure is printed
# the moment it happens; the other lanes finish before the run gives up.
{
  step "dotnet format" "$BACKEND" dotnet format --verify-no-changes &&
    step "dotnet build" "$BACKEND" dotnet build --no-restore &&
    step "contract" "$BACKEND" contract &&
    step "domain unit tests" "$BACKEND" dotnet test tests/Domain.UnitTests --no-build &&
    step "application unit tests" "$BACKEND" dotnet test tests/Application.UnitTests --no-build &&
    step "architecture tests" "$BACKEND" dotnet test tests/ArchitectureTests --no-build &&
    step "integration tests" "$BACKEND" dotnet test tests/IntegrationTests --no-build
} &
LANES="$LANES $!"

{
  step "pnpm lint" "$FRONTEND" pnpm lint &&
    step "pnpm check" "$FRONTEND" pnpm check &&
    step "pnpm test:unit" "$FRONTEND" pnpm test:unit &&
    step "pnpm build" "$FRONTEND" pnpm build &&
    step "pnpm verify:release" "$FRONTEND" pnpm verify:release &&
    step "pnpm verify:budget" "$FRONTEND" pnpm verify:budget
} &
LANES="$LANES $!"

{
  step "image" "$TREE" docker build -t culina:ci-local --build-arg REVISION="$SHORT" . &&
    step "stack" "$TREE" compose up -d --wait &&
    step "database setup" "$TREE" env Database__Password=culina_ci_password \
      scripts/setup-database.sh "$BASE" &&
    step "first account" "$TREE" first_account
} &
LANES="$LANES $!"

failed=0
for lane in $LANES; do
  wait "$lane" || failed=1
done
LANES=""
[ "$failed" -eq 0 ] || exit 1

# After the frontend lane, not beside it: the suite builds the app into the
# same directories, and compiles the same messages.
step "playwright browser" "$FRONTEND" pnpm exec playwright install chromium
step "end to end" "$FRONTEND" env CI=true \
  CULINA_API="$BASE" CULINA_IMAGE_URL="$BASE" \
  CULINA_E2E_EMAIL="$EMAIL" CULINA_E2E_PASSWORD="$PASSWORD" \
  pnpm test:e2e

printf '\033[32mci-local: %s passes, in %dm%02ds.\033[0m\n' "$SHORT" $((SECONDS / 60)) $((SECONDS % 60))
