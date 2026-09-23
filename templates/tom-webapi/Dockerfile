# Multi-stage build for the stamped API.
# `dotnet new` replaces the Tom.WebApi source name in this file.
# Listens on 8080. The entrypoint starts as root only to chown the
# keys/ and emails/ mounts, then drops to the image's non-root app user.
# TLS stays at the reverse proxy.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Tom.WebApi.csproj \
    && dotnet publish Tom.WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM build AS migrate
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "Tom.WebApi.csproj", "--no-launch-profile"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build --chown=app:app /app/publish .
COPY docker-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh \
    && mkdir -p /app/keys /app/emails \
    && chown app:app /app/keys /app/emails

USER root
ENTRYPOINT ["/entrypoint.sh"]
