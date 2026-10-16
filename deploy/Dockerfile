# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
ARG TARGETARCH
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props LICENSE.md ./
COPY licenses/ licenses/
COPY src/AmorRP.Core/ src/AmorRP.Core/
COPY src/AmorRP.Contracts/ src/AmorRP.Contracts/
COPY src/AmorRP.Server/ src/AmorRP.Server/
# Optional combined CA bundle for development environments with an HTTPS proxy.
# It is mounted for this step only, and never copied into a layer.
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export SSL_CERT_FILE=/run/secrets/proxy_ca; fi; \
    case "${TARGETARCH:-amd64}" in amd64) rp_rid=linux-x64 ;; arm64) rp_rid=linux-arm64 ;; *) echo "Unsupported server architecture" >&2; exit 1 ;; esac; \
    dotnet restore src/AmorRP.Server/AmorRP.Server.csproj --locked-mode && \
    dotnet publish src/AmorRP.Server/AmorRP.Server.csproj -c Release --no-restore -r "$rp_rid" --self-contained false -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY --chmod=0644 deploy/start-server.sh /app/start-server.sh
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["/bin/sh", "/app/start-server.sh"]
