# Culina developer commands. Every target is the real command, not a wrapper
# script, so it is always obvious what is being run.

# bash rather than whatever /bin/sh is today, because the server targets below
# need job control and `set -m` is not in POSIX sh.
SHELL := /bin/bash

BACKEND  := src/backend
FRONTEND := src/frontend
API      := $(BACKEND)/src/Api

# Stops a server and everything it started.
#
# Ctrl+C used to leave the API running. `dotnet watch` ignores INT and TERM —
# not "eventually shuts down", ignores them — so the signal reached the watcher
# and nothing else, and the app it had launched went on holding port 5000 with
# no terminal attached to it. The next `make backend` then could not bind, and
# the only way out was hunting the process down by port.
#
# So the signal goes to the process group rather than to the command: `set -m`
# puts each server in a group of its own, and `kill -- -$$pid` reaches the
# watcher, the `dotnet run` under it and the app under that. TERM first, so a
# server that does clean up gets to; KILL two seconds later for the one that
# does not.
#
# Job control is also why every server reads from /dev/null. A background job
# that reads the terminal is sent SIGTTIN and stopped, and `dotnet watch` reads
# stdin — so without this it suspends before it ever binds the port, and the
# frontend answers every /api call with ECONNREFUSED while the watcher sits
# there in state T looking for all the world like it is running. Nothing here
# wants the keyboard: the watcher is already told --non-interactive.
define stop
	kill -TERM -$$1 2>/dev/null; sleep 2; kill -KILL -$$1 2>/dev/null; true
endef

.DEFAULT_GOAL := help
.PHONY: help dev db-up db-down db-reset db-shell backend frontend \
        build test test-backend test-frontend test-e2e lint format api \
        image image-run clean

help: ## Show this help
	@grep -hE '^[a-z0-9-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-14s\033[0m %s\n", $$1, $$2}'

# ── Running ──────────────────────────────────────────────────────────────────

dev: db-up ## Start the database, the API and the frontend dev server
	@echo "API      http://localhost:5000"
	@echo "Frontend http://localhost:5173  (proxies /api to the API)"
	@set -m; \
		(cd $(API) && exec dotnet watch run --non-interactive < /dev/null) & api=$$!; \
		(cd $(FRONTEND) && exec pnpm exec vite dev --host < /dev/null) & web=$$!; \
		trap 'stop() { $(stop) ; }; stop $$api; stop $$web; exit 0' INT TERM; \
		wait

backend: db-up ## Run only the API, with hot reload
	@set -m; \
		(cd $(API) && exec dotnet watch run --non-interactive < /dev/null) & api=$$!; \
		trap 'stop() { $(stop) ; }; stop $$api; exit 0' INT TERM; \
		wait $$api

frontend: ## Run only the frontend dev server
	@set -m; \
		(cd $(FRONTEND) && exec pnpm dev < /dev/null) & web=$$!; \
		trap 'stop() { $(stop) ; }; stop $$web; exit 0' INT TERM; \
		wait $$web

# ── Database ─────────────────────────────────────────────────────────────────

db-up: ## Start PostgreSQL and wait until it is healthy
	docker compose up -d --wait db

db-down: ## Stop PostgreSQL, keeping its data
	docker compose stop db

db-reset: ## Destroy the database and recreate it from scratch
	docker compose down -v
	docker compose up -d --wait db

db-shell: ## Open psql as the application role
	docker compose exec db psql -U culina_app -d culina

# ── Building ─────────────────────────────────────────────────────────────────

build: ## Build both halves
	cd $(BACKEND) && dotnet build
	cd $(FRONTEND) && pnpm build

# ── Testing ──────────────────────────────────────────────────────────────────

test: test-backend test-frontend ## Run every test except the end-to-end suite

test-backend: db-up ## Run the four backend test projects
	cd $(BACKEND) && dotnet test

test-frontend: ## Run the frontend unit tests
	cd $(FRONTEND) && pnpm test:unit

test-e2e: ## Run the Playwright suite against a running stack
	@# Suites tagged @offline need nothing but the built app. The signed-in
	@# ones skip unless an account is supplied, because a test that quietly
	@# passes with no backend is worse than one that says it did not run:
	@#   CULINA_E2E_EMAIL=you@example.com CULINA_E2E_PASSWORD=... make test-e2e
	@# The first-run suite needs that account to be an administrator — it opens
	@# registration before it can register anyone.
	cd $(FRONTEND) && pnpm test:e2e

# ── Quality ──────────────────────────────────────────────────────────────────

lint: ## Verify formatting and lint rules in both halves
	cd $(BACKEND) && dotnet format --verify-no-changes
	cd $(FRONTEND) && pnpm lint

format: ## Apply formatting to both halves
	cd $(BACKEND) && dotnet format
	cd $(FRONTEND) && pnpm format

# ── API contract ─────────────────────────────────────────────────────────────

api: openapi ## Export the OpenAPI document and regenerate the frontend client
	cd $(FRONTEND) && pnpm generate:api
	@echo "Regenerated. Commit openapi/Api.json together with the client — CI"
	@echo "fails if the committed client does not match the document."

openapi: db-up ## Export the OpenAPI document to src/backend/openapi/Api.json
	cd $(API) && dotnet run -- --export-openapi ../../openapi/Api.json

# ── Container ────────────────────────────────────────────────────────────────

image: ## Build the production container image
	docker build -t culina:local .

image-run: ## Run the production image against the dev database
	docker compose -f compose.yaml -f compose.prod.yaml up --build

clean: ## Remove build output from both halves
	cd $(BACKEND) && dotnet clean
	rm -rf $(FRONTEND)/build $(FRONTEND)/.svelte-kit
