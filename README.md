# Pub.dev Package Intelligence MCP

A Model Context Protocol (MCP) server that wraps the official pub.dev APIs so assistants can discover Dart and Flutter packages, inspect versions, validate compatibility, explore dependency trees, and review score insights without leaving their chat environment.

## Highlights

- Eight MCP tools (`search_packages`, `latest_version`, `check_compatibility`, `list_versions`, `package_details`, `publisher_packages`, `score_insights`, `dependency_inspector`).
- NativeAOT-ready .NET 9 implementation with Clean Architecture slices.
- Resilient HTTP client with retries, circuit breaking, and caching.
- Structured logging with Serilog and full OpenTelemetry traces/metrics.
- Dual transports: stdio for local assistants and HTTP+JSON-RPC for remote deployment.

## Prerequisites

- .NET 9 SDK (9.0.0-preview or newer with C# 13)
- PowerShell 7+ or Windows PowerShell 5.1 for helper scripts
- Access to the pub.dev REST API over HTTPS
- For NativeAOT publish on Windows: Visual Studio Build Tools with the **C++ Desktop Development** workload and Windows 11 SDK installed

Refer to [`specs/001-build-an-mcp/quickstart.md`](specs/001-build-an-mcp/quickstart.md) for expanded setup details and troubleshooting tips.

## Getting Started

### Clone & Restore

```powershell
# From the directory where you keep repos
git clone <your-public-repo-url>
cd pub_dev_mcp

dotnet restore PubDevMcp.sln
```

### Run via stdio (default)

```powershell
dotnet run --project src/PubDevMcp.Server/PubDevMcp.Server.csproj -- --stdio
```

### Run via HTTP

```powershell
$env:ASPNETCORE_URLS="http://localhost:5111";
dotnet run --project src/PubDevMcp.Server/PubDevMcp.Server.csproj -- --http
```

The HTTP transport exposes:
- `POST /rpc` for JSON-RPC requests
- `GET /health/live` and `GET /health/ready` for health probes

### NativeAOT Publish (Self-Contained)

```powershell
dotnet publish src/PubDevMcp.Server/PubDevMcp.Server.csproj `
  -c Release `
  -r win-x64 `
  /p:PublishAot=true /p:SelfContained=true /p:PublishTrimmed=true /p:InvariantGlobalization=true `
  --no-restore
```

> **Note:** Cross-OS NativeAOT publishing isnt supported. Always publish on the same OS you target.

The publish output lives under `src/PubDevMcp.Server/bin/Release/net9.0/<RID>/publish/`. Copy the contents to your deployment target.

## Testing

```powershell
# Contract and schema checks
dotnet test tests/contract/PubDevMcp.Tests.Contract.csproj --configuration Release

# Integration coverage
dotnet test tests/integration/PubDevMcp.Tests.Integration.csproj --configuration Release

# Compliance harness
dotnet test tests/compliance/PubDevMcp.Tests.Compliance.csproj --configuration Release
```

Benchmark profiles for NFR validation live in `tests/performance/` and can be executed with:

```powershell
cd tests/performance
dotnet run --configuration Release
```

## Registry Submission Snapshot

- **Transports:** stdio, http
- **License:** MIT (add `LICENSE` file before publishing)
- **Maintainer Contact:** <maintainer@example.com>
- **Upstream Docs:** [`quickstart.md`](specs/001-build-an-mcp/quickstart.md)

Prior to submission:
1. Promote this code to a public repository with a stable `main` branch.
2. Tag a release (e.g., `v1.0.0`) built with NativeAOT binaries.
3. Attach Release build/test logs and the quickstart link to the release notes.
4. Complete the GitHub MCP registry metadata form (see `specs/001-build-an-mcp/registry-metadata.yaml`).

## Architectural Overview

```
src/
  PubDevMcp.Domain        // Immutable domain models
  PubDevMcp.Application   // MediatR handlers + validation
  PubDevMcp.Infrastructure// HTTP client, caching, logging, telemetry
  PubDevMcp.Server        // JsonRpc pipeline, transports, DI wiring

tests/
  contract/               // OpenAPI + tool contract verification
  compliance/             // JSON-RPC compliance tests
  integration/            // Transport + observability integration tests
  performance/            // BenchmarkDotNet suite (p95 <= 7s guardrail)
```

## Support & Feedback

Questions or issues? Open a GitHub issue or reach out to the maintainer listed in the registry metadata. Contributions are welcome via pull requests once the repository is public.
