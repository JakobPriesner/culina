# The push relay (docs/native-app-design.md, part 7): run by whoever publishes
# the iOS and Android apps, never part of Culina's own image. Build from the
# repository root:  docker build -f deploy/relay/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build

WORKDIR /src

# The build applies the repository's style rules as errors, so it needs them.
COPY .editorconfig ./
COPY src/backend/Directory.Build.props src/backend/Directory.Packages.props src/backend/global.json ./
COPY src/backend/src/Relay/Relay.csproj src/backend/src/Relay/packages.lock.json src/Relay/

RUN dotnet restore src/Relay/Relay.csproj --locked-mode

COPY src/backend/src/Relay/ src/Relay/

RUN dotnet publish src/Relay/Relay.csproj --configuration Release --no-restore --output /app

# The one volume: the relay token map. Made here, owned by the app user below,
# because the chiseled image has no shell to make it in.
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:84def277556c2843a44fdeefbd3145d0a2e32d8f736d0c1a7b0a96b2880b0187

WORKDIR /app

COPY --from=build /app ./
COPY --from=build --chown=$APP_UID:$APP_UID /data /data

ENV Relay__DatabasePath=/data/relay.db \
    ASPNETCORE_HTTP_PORTS=8080

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Relay.dll"]
