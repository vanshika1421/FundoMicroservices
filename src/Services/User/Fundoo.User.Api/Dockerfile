FROM mcr.microsoft.com/dotnet/sdk:8.0.428-bookworm-slim AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj
RUN dotnet publish src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:8.0.428-bookworm-slim AS migrator
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj \
    && dotnet tool install --global dotnet-ef --version 8.0.11
ENV PATH="$PATH:/root/.dotnet/tools" \
    ConnectionStrings__UserDb="Host=postgres-service;Port=5432;Database=fundoo_users;Username=postgres"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/Services/User/Fundoo.User.Infrastructure", "--startup-project", "src/Services/User/Fundoo.User.Api"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0.34-bookworm-slim AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build --chown=$APP_UID:$APP_UID /out .
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Fundoo.User.Api.dll"]
