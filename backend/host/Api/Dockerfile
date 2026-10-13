# syntax=docker/dockerfile:1

# Stage 1: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN mkdir -p /var/lib/ugtm/data-protection-keys \
    && chown -R $APP_UID:$APP_UID /var/lib/ugtm
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Stage 2: SDK build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy entire backend source
COPY . .

WORKDIR "/src/host/Api"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "UGTM.Api.csproj"

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet build "UGTM.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "UGTM.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Production runner
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "UGTM.Api.dll"]
