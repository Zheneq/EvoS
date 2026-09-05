# Lobby image: the EvoS.Sandbox process (DirectoryServer + LobbyServer2 + admin/user
# REST APIs + Prometheus) published as a self-contained single-file linux-x64 build.
# Published to ghcr.io/zheneq/evos — see .github/workflows/docker-lobby.yml.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against just the project files first so the layer is cached until a
# .csproj changes. EvoS.Sandbox transitively references the rest of the graph.
COPY EvoS.Framework/EvoS.Framework.csproj EvoS.Framework/
COPY EvoS.DirectoryServer/EvoS.DirectoryServer.csproj EvoS.DirectoryServer/
COPY LobbyServer2/CentralServer.csproj LobbyServer2/
COPY EvoS.Sandbox/EvoS.Sandbox.csproj EvoS.Sandbox/
RUN dotnet restore EvoS.Sandbox/EvoS.Sandbox.csproj -r linux-x64

# Copy the rest of the sources and publish. NOT trimmed: EvosSerializer reflects
# over the assemblies at startup, so trimming would strip message types.
COPY . .
RUN dotnet publish EvoS.Sandbox/EvoS.Sandbox.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        --no-restore \
        -p:PublishSingleFile=true \
        -o /app

# ---- runtime ----
# runtime-deps is enough because the publish is self-contained (bundles the
# .NET + ASP.NET Core runtime). Debian bookworm-based, ships the ICU deps .NET needs.
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Transport (fronted by internal-nginx) + REST APIs + Prometheus. Documentation only.
EXPOSE 6050 6060 3001 3002 1234

ENTRYPOINT ["./EvoS.Sandbox"]