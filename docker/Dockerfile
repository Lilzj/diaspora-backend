# ─── Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY Diaspora.slnx .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .
COPY .editorconfig .

# Every project file, not a hand-maintained subset.
#
# This used to name ten projects explicitly, and the solution has thirty-three. The
# failure that caused is worth recording, because nothing about it looked like a
# failure: `dotnet restore` does not error on a ProjectReference whose file is absent
# from the build context. It prints `Skipping project "..." because it was not found`
# and **exits 0**, having written an assets file for the Api that resolves none of the
# twenty-three missing projects. The `COPY src/ src/` below then brings them all in and
# `dotnet publish --no-restore` dies on the first one:
#
#     error NETSDK1004: Assets file '/source/src/Diaspora.Analyzers/obj/project.assets.json'
#     not found. Run a NuGet package restore to generate this file.
#
# So the image could not be built at all, while the layer that was wrong reported
# success. Every project added since the list was written widened the break, and none
# of them had any reason to touch this file.
#
# A wildcard cannot go stale that way. It costs the restore layer some cache
# granularity — the layer is invalidated when *any* project file changes rather than
# one of ten — which is a repeated restore, not a broken image.
#
# `COPY src/*/*.csproj ./` flattens: Docker discards the directory part of a wildcard
# source, so all thirty-three land in /source. The loop puts each one back under the
# directory its own name implies, which is the convention this repository already
# follows for every project without exception.
COPY src/*/*.csproj ./
RUN for project in *.csproj; do \
        name="${project%.csproj}"; \
        mkdir -p "src/$name"; \
        mv "$project" "src/$name/"; \
    done

RUN dotnet restore src/Diaspora.Api/Diaspora.Api.csproj

COPY src/ src/

# `-maxcpucount:1`, because the builder runs out of memory before it runs out of cores.
#
# The first attempt at v108 (17 Sep 2026) died with `csc exited with code 137` — the Linux
# out-of-memory kill — compiling Diaspora.Persistence. A plain retry succeeded, which is what
# a memory ceiling looks like: MSBuild defaults to one node per core, Fly's remote builder has
# far more cores than it has gigabytes per core, and thirty-three projects compiling at once
# each hold a Roslyn compilation in memory. Two nodes still overlap the long projects with the
# short ones and keep the peak inside the machine. One node since 15 Oct 2026: with the 14 Oct
# migrations Diaspora.Persistence alone needs most of the builder's memory, and two nodes were
# killed (137) twice running.
#
# `UseSharedCompilation=false` for the same reason: the compiler server survives between
# projects holding what it has already loaded, which is a saving in time and a cost in memory
# on a build that runs once per deploy.
RUN dotnet publish src/Diaspora.Api/Diaspora.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -maxcpucount:1 \
    -p:UseSharedCompilation=false

# ─── Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ── Non-root, using the user the base image already ships ──────────────────
#
# This used to run `addgroup --system diaspora && adduser --system …`. Those are
# BusyBox applets: they exist on Alpine and **not** on this image, which is
# Debian-based and slim enough to omit the `adduser` package. The build died on
# that line with `/bin/sh: 1: addgroup: not found`.
#
# Worth stating plainly, because the file gave no hint of it: **this Dockerfile
# had never built successfully.** It carries a detailed comment about an earlier
# `COPY` failure, so it reads as maintained — but nothing had ever run it far
# enough to reach line three of the runtime stage.
#
# Rather than swap in Debian's `groupadd`/`useradd`, this uses the `app` user
# (UID 1654) that Microsoft has shipped in the .NET runtime images since .NET 8
# precisely so images do not have to create one. `--chown` on the COPY replaces
# the separate `chown -R`, which is one fewer layer and one fewer full-tree walk.
COPY --from=build --chown=app:app /app/publish .
USER app

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
