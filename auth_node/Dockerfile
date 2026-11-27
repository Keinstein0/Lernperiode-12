# ── Build: Native AOT needs clang and zlib next to the SDK ──────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY src/AuthNode/AuthNode.csproj src/AuthNode/
RUN dotnet restore src/AuthNode/AuthNode.csproj -r linux-x64
COPY src/AuthNode/ src/AuthNode/
RUN dotnet publish src/AuthNode/AuthNode.csproj -c Release -r linux-x64 --no-restore -o /app \
    && rm -f /app/*.dbg
# data folder owned by the non-root user of the runtime image (uid 1654)
RUN mkdir /data && chown 1654:1654 /data

# ── Run: minimal image, no shell, no package manager, non-root ──────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/AuthNode /app/libe_sqlite3.so /app/appsettings.json ./
COPY --from=build --chown=1654:1654 /data /data
ENV AUTH_DB_PATH=/data/auth.db \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
ENTRYPOINT ["./AuthNode"]
