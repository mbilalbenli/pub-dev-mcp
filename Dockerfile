# syntax=docker/dockerfile:1.7

ARG BUILD_CONFIGURATION=Release
ARG TARGETOS=linux
ARG TARGETARCH=amd64

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0 AS restore
WORKDIR /src

COPY Directory.Build.props ./
COPY global.json ./
COPY src/PubDevMcp.Domain/PubDevMcp.Domain.csproj src/PubDevMcp.Domain/
COPY src/PubDevMcp.Application/PubDevMcp.Application.csproj src/PubDevMcp.Application/
COPY src/PubDevMcp.Infrastructure/PubDevMcp.Infrastructure.csproj src/PubDevMcp.Infrastructure/
COPY src/PubDevMcp.Server/PubDevMcp.Server.csproj src/PubDevMcp.Server/

RUN dotnet restore src/PubDevMcp.Server/PubDevMcp.Server.csproj --runtime ${TARGETOS}-${TARGETARCH} --nologo

FROM restore AS publish
COPY . ./
RUN dotnet publish src/PubDevMcp.Server/PubDevMcp.Server.csproj \
    -c ${BUILD_CONFIGURATION} \
    -r ${TARGETOS}-${TARGETARCH} \
    -o /app/publish \
    /p:PublishAot=true \
    /p:SelfContained=true \
    /p:PublishTrimmed=true \
    /p:InvariantGlobalization=true \
    --no-restore \
    --nologo

FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:9.0-noble AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --gecos "" mcp

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    COMPlus_EnableDiagnostics=0

COPY --from=publish /app/publish ./

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s \
    CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1

USER mcp

ENTRYPOINT ["./PubDevMcp.Server"]
