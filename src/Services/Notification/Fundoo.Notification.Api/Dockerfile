FROM mcr.microsoft.com/dotnet/sdk:8.0.428-bookworm-slim AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj
RUN dotnet publish src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false

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
ENTRYPOINT ["dotnet", "Fundoo.Notification.Api.dll"]
