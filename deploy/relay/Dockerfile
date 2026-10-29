# The push relay (docs/native-app-design.md, part 7): run by whoever publishes
# the iOS and Android apps, never part of Culina's own image. Build from the
# repository root:  docker build -f deploy/relay/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build

WORKDIR /src

# The build applies the repository's style rules as errors, so it needs them.
COPY .editorconfig ./
COPY src/backend/Directory.Build.props src/backend/Directory.Packages.props src/backend/global.json ./
# Names the runtimes the lock file was restored for; without it the locked restore refuses.
COPY src/backend/src/Directory.Build.props src/
COPY src/backend/src/Relay/Relay.csproj src/backend/src/Relay/packages.lock.json src/Relay/

RUN dotnet restore src/Relay/Relay.csproj --locked-mode

COPY src/backend/src/Relay/ src/Relay/

RUN dotnet publish src/Relay/Relay.csproj --configuration Release --no-restore --output /app

# The one volume: the relay token map. Made here, owned by the app user below,
# because the chiseled image has no shell to make it in.
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d

WORKDIR /app

COPY --from=build /app ./
COPY --from=build --chown=$APP_UID:$APP_UID /data /data

ENV Relay__DatabasePath=/data/relay.db \
    ASPNETCORE_HTTP_PORTS=8080

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Relay.dll"]
