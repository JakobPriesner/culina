# Culina, as one image.
#
# The API and the built SPA are served from the same origin by the same
# process. That is not a packaging convenience: it is what removes CORS from
# the system entirely and makes the cookie and CSRF model sound. An image that
# served the two halves from different origins would need a different, weaker
# auth design.
#
# Three stages, so nothing that built the app is in what ships: no Node, no
# .NET SDK, no shell, no package manager.

# ── 1. The app ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS frontend

WORKDIR /src

# Corepack pins pnpm to the version in package.json's `packageManager`, so the
# image is built by the same pnpm the lockfile was written by.
RUN corepack enable

# Manifests first, on their own layer: they change far less often than the
# source, so an ordinary code change reuses the install.
COPY src/frontend/package.json src/frontend/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile --ignore-scripts

# The client is generated from the committed OpenAPI document, never from a
# running server, so building the image needs no database and no backend.
COPY src/backend/openapi/Api.json ../backend/openapi/Api.json
COPY src/frontend/ ./
RUN pnpm generate:api && pnpm build

# ── 2. The server ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend

WORKDIR /src

# The repository's .editorconfig, because the build applies its style rules:
# EnforceCodeStyleInBuild plus warnings-as-errors means a missing .editorconfig
# is a failed build, not a differently formatted one.
COPY .editorconfig ./

# Same idea: every project file and lock file before any source.
COPY src/backend/Directory.Build.props src/backend/Directory.Packages.props \
     src/backend/global.json src/backend/backend.slnx ./
COPY src/backend/src/Domain/Domain.csproj src/backend/src/Domain/packages.lock.json src/Domain/
COPY src/backend/src/Contracts/Contracts.csproj src/backend/src/Contracts/packages.lock.json src/Contracts/
COPY src/backend/src/Application/Application.csproj src/backend/src/Application/packages.lock.json src/Application/
COPY src/backend/src/Infrastructure/Infrastructure.csproj src/backend/src/Infrastructure/packages.lock.json src/Infrastructure/
COPY src/backend/src/Api/Api.csproj src/backend/src/Api/packages.lock.json src/Api/

# Locked: a build that silently resolves a different version of a dependency is
# not the build that was tested.
RUN dotnet restore src/Api/Api.csproj --locked-mode

COPY src/backend/src/ src/

# The SPA is served by the same host, from wwwroot.
COPY --from=frontend /src/build/ src/Api/wwwroot/

RUN dotnet publish src/Api/Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app

# The two data directories, made here because the runtime image has no shell to
# make them in. Docker seeds a volume's mount point from what the image already
# has at that path — including its ownership — so without this the volumes
# arrive owned by root and the non-root app cannot write a single photograph.
RUN mkdir -p /data/images /data/keys

# ── 3. What ships ────────────────────────────────────────────────────────────
# Chiseled: no shell and no package manager, which is both a much smaller
# attack surface and a much smaller CVE feed to keep up with.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime

# Traceable to a commit, from `docker inspect`, without asking anyone.
ARG REVISION=unknown
ARG VERSION=0.0.0-dev

LABEL org.opencontainers.image.title="Culina" \
      org.opencontainers.image.description="Your recipes, the way you cook them." \
      org.opencontainers.image.licenses="AGPL-3.0-or-later" \
      org.opencontainers.image.revision="${REVISION}" \
      org.opencontainers.image.version="${VERSION}"

WORKDIR /app

COPY --from=backend /app ./

# Owned by the user that runs, for the reason above.
COPY --from=backend --chown=$APP_UID:$APP_UID /data /data

# Both must be volumes. Recipe images are the data that cannot be rebuilt, and
# the key ring is what the framework protects anything else with. The
# directories are created here so a deployment that forgets to mount them still
# starts — and the app says so in its logs rather than failing at the first
# upload.
ENV Storage__ImagePath=/data/images \
    Storage__DataProtectionKeyPath=/data/keys \
    ASPNETCORE_HTTP_PORTS=8080 \
    # Nothing in this image reads a user's locale, and the culture data is a
    # meaningful part of a chiseled image's size. Culina formats every
    # user-facing value on the client, where the user's own locale is.
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

VOLUME ["/data/images", "/data/keys"]

EXPOSE 8080

# The non-root user the chiseled image ships with. Declared by name for the
# same reason the ports are: so `docker inspect` answers the question.
USER $APP_UID

# /health/ready is the one that touches the database; /health/live would say a
# process exists, which is not the question a load balancer is asking.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD ["/app/Api", "--health-check"]

ENTRYPOINT ["/app/Api"]
