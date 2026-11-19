# syntax=docker/dockerfile:1

# Stage 1: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN mkdir -p /var/lib/ugtm/data-protection-keys \
    && chown -R $APP_UID:$APP_UID /var/lib/ugtm
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Keep only project metadata in this stage. Its output stays identical on source-only edits.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS project-files
WORKDIR /src
COPY . .
RUN find . -type f ! -name '*.csproj' ! -name '*.props' ! -name '*.targets' ! -iname 'nuget.config' ! -name 'packages.lock.json' -delete

# Stage 2: SDK build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY --from=project-files /src/ .

WORKDIR "/src/host/Api"
# Keep packages in the exported layer: GHA cache does not export cache-mount contents.
RUN dotnet restore "UGTM.Api.csproj" -p:Configuration=$BUILD_CONFIGURATION

COPY . /src/

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UGTM.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

# Stage 4: Production runner
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "UGTM.Api.dll"]
