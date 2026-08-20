# ─── Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY Diaspora.slnx .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .
COPY .editorconfig .

COPY src/Diaspora.SharedKernel/Diaspora.SharedKernel.csproj src/Diaspora.SharedKernel/
COPY src/Diaspora.Contracts/Diaspora.Contracts.csproj src/Diaspora.Contracts/
COPY src/Diaspora.Domain/Diaspora.Domain.csproj src/Diaspora.Domain/
COPY src/Diaspora.Platform/Diaspora.Platform.csproj src/Diaspora.Platform/
COPY src/Diaspora.Application/Diaspora.Application.csproj src/Diaspora.Application/
COPY src/Diaspora.Persistence/Diaspora.Persistence.csproj src/Diaspora.Persistence/
COPY src/Diaspora.Infrastructure/Diaspora.Infrastructure.csproj src/Diaspora.Infrastructure/
COPY src/Diaspora.Observability/Diaspora.Observability.csproj src/Diaspora.Observability/
COPY src/Diaspora.Modules.Bookmarks/Diaspora.Modules.Bookmarks.csproj src/Diaspora.Modules.Bookmarks/
COPY src/Diaspora.Api/Diaspora.Api.csproj src/Diaspora.Api/

RUN dotnet restore src/Diaspora.Api/Diaspora.Api.csproj

COPY src/ src/
RUN dotnet publish src/Diaspora.Api/Diaspora.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: run as non-root
RUN addgroup --system diaspora && adduser --system --ingroup diaspora diaspora

COPY --from=build /app/publish .
RUN chown -R diaspora:diaspora /app
USER diaspora

# The aspnet runtime image ships neither curl nor wget, so the previous
# `curl -f .../health/live` probe could only ever report unhealthy. The published
# app is its own probe instead: `--health-check` calls the liveness endpoint
# in-process and exits non-zero on failure.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD ["dotnet", "Diaspora.Api.dll", "--health-check"]

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Diaspora.Api.dll"]
