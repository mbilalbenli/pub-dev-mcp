# Implementation Plan: Pub.dev Package Intelligence MCP

**Branch**: `001-build-an-mcp` | **Date**: 2025-09-25 | **Spec**: [`spec.md`](spec.md)
**Input**: Feature specification from `specs/001-build-an-mcp/spec.md`

## Summary
Design and deliver a C# 13/.NET 9 MCP server that exposes eight tools for pub.dev intelligence. Each tool wraps an official pub.dev REST endpoint, enforces validated inputs, respects the 10-result search cap, retries transient failures three times with exponential backoff, and emits structured telemetry so assistants can quickly retrieve package discovery, versioning, compatibility, scoring, and dependency insights.

## Technical Context
**Language/Version**: C# 13 on .NET 9.0 (AOT-ready)  
**Primary Dependencies**: `System.Net.Http`, Polly resilience policies, Serilog with source generators, OpenTelemetry SDK/Exporter, MediatR (vertical slices), FluentValidation (source generated), `Microsoft.Extensions.Caching.Memory`, `System.Text.Json` for schema validation, BenchmarkDotNet for NFR validation  
**Storage**: No persistent datastore; short-lived in-memory caches (`IMemoryCache`) for score and dependency memoization  
**Testing**: xUnit with FluentAssertions, `WebApplicationFactory`/TestHost for transport, JSON-RPC compliance suite, Approval tests for contract snapshots  
**Target Platform**: Cross-platform MCP server shipping as stdio host for local tools and Streamable HTTP for containerized deployments (Linux container baseline)  
**Project Type**: Single service (Clean Architecture vertical slices)  
**Performance Goals**: ≤5.0s typical and ≤7.0s p95 end-to-end per spec; target <200ms internal processing (excluding pub.dev latency) using pooled buffers and `System.IO.Pipelines`  
**Constraints**: Enforce 10-result limit, 3 retry attempts with jittered exponential backoff, redact logs with 14-day retention, respect pub.dev rate limits and SDK constraint semantics  
**Scale/Scope**: One MCP server exposing eight tools, contract + integration test suites, observability pipeline, and deployment scripts for container and local modes

## Constitution Check
*Initial and post-design checks complete; no deviations required.*

- [x] **Security-First MCP Integration**:
  - OAuth 2.1 with PKCE for Streamable HTTP transport; stdio mode requires local trust authority.
  - JSON schema validation for each tool request/response using `System.Text.Json` source generators.
  - RBAC scopes guarded via MCP tool capability map and request digests; global rate limiter via `RateLimiterOptions` + Polly bulkhead.
  - Signed release pipeline with SBOM and dependency pinning; supply-chain alerts wired through GitHub Dependabot.
- [x] **Performance & Resilience Discipline**:
  - Central HTTP client using `System.Net.Http.SocketsHttpHandler` with `System.IO.Pipelines` buffering.
  - Polly policies: timeout (3s), retry (3 attempts, jittered exponential), and circuit breaker (5 failures/30s) per pub.dev host.
  - Exception pipeline normalizes errors to JSON-RPC codes and maps to Problem Details.
  - BenchmarkDotNet harness planned to enforce ≤7s p95 including network, plus microbenchmarks for parsing logic.
- [x] **Observable Operations & Traceability**:
  - Serilog JSON sink with source-generated `LoggerMessage` for hot paths.
  - OpenTelemetry traces (activity per MCP request), metrics (success/failure counters, latency histograms), and health checks at `/health/live` and `/health/ready`.
  - Audit logs hashing requests/responses (per FR-006) stored to rolling files within 14-day retention window.
- [x] **Architectural Integrity & Modularity**:
  - Clean Architecture layering: `Domain`, `Application`, `Infrastructure`, `Presentation` with vertical slices per tool via MediatR handlers.
  - Tool exposure through `[McpServerTool]` attributes and DI modules; Options pattern with strongly typed settings for pub.dev, resilience, telemetry.
  - Feature toggles isolated via Options + `IOptionsSnapshot` to maintain hot reload without reboots.
- [x] **Test & Compliance Driven Delivery**:
  - Contract-first tests (Approval/xUnit) per endpoint, integration tests via TestHost (stdio) and WebSocket harness (HTTP) failing initially.
  - JSON-RPC compliance suite covering method names and structured errors.
  - Security/perf gates integrated in CI (OWASP dependency check, `dotnet test`, BenchmarkDotNet smoke), ensuring Definition of Done alignment.
- [x] Constitution deviations captured with mitigation tasks or noted as “None required”.

## Project Structure

### Documentation (feature scope)
```
specs/001-build-an-mcp/
├── plan.md          # This plan
├── research.md      # Phase 0 decisions
├── data-model.md    # Entities and validation rules
├── quickstart.md    # Spin-up & test guide
├── contracts/
│   └── pubdev-mcp-openapi.yaml
└── tasks.md         # Produced by /tasks (pending)
```

### Source Code Layout
```
src/
├── PubDevMcp.Server/            # Presentation + hosting (stdio & HTTP)
├── PubDevMcp.Application/       # MediatR handlers, validators, DTO mappers
├── PubDevMcp.Domain/            # Core entities, value objects, policies
└── PubDevMcp.Infrastructure/    # Http clients, resilience policies, telemetry

tests/
├── contract/                    # Approval + schema tests per tool
├── integration/                 # End-to-end MCP invocation suites
├── compliance/                  # JSON-RPC + security checks
└── performance/                 # BenchmarkDotNet projects
```

**Structure Decision**: Option 1 (single service) with Clean Architecture slices; additional projects created only as above to respect modularity.

## Phase 0: Outline & Research
1. Completed research recorded in [`research.md`](research.md):
   - Confirmed official pub.dev endpoints for each tool and ruled out HTML scraping.
   - Documented compatibility evaluation strategy using pubspec `environment.sdk` semantics.
   - Selected Serilog, OpenTelemetry, Polly, FluentValidation, and caching approach consistent with constitution mandates.
   - Established audit logging hashes and redaction rules for FR-006 compliance.
2. No unresolved clarifications remain; all spec ambiguities closed in `/clarify` session.
3. Research readiness gate PASSED → proceed to design.

## Phase 1: Design & Contracts
1. Authored [`data-model.md`](data-model.md) capturing PackageSummary, VersionDetail, CompatibilityResult, DependencyGraph, ScoreInsight, and AuditLogEntry relationships with validation rules.
2. Defined OpenAPI contract [`contracts/pubdev-mcp-openapi.yaml`](contracts/pubdev-mcp-openapi.yaml) mapping each MCP tool to POST endpoints with schema-validated request/response bodies and shared Problem Details.
3. Planned contract-first test scaffolding:
   - Approval tests verifying OpenAPI snapshots for `searchPackages`, `checkCompatibility`, etc.
   - Schema validation tests ensuring 10-result cap and retry hints surface correctly.
4. Derived integration scenarios for quickstart + test harness:
   - Search flow returning top 10 with follow-up guidance.
   - Compatibility evaluation covering satisfied/unsatisfied paths.
   - Dependency inspection depth cap and conflict warnings.
5. Authored [`quickstart.md`](quickstart.md) with stdio launch instructions, sample test runs, telemetry configuration, and cleanup guidance.
6. Ran `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot` (will re-run after plan saves to refresh tech markers).
7. Phase 1 gate PASSED; ready for task planning.

## Phase 2: Task Planning Approach
- `/tasks` will derive tasks from the design artifacts:
  - Each OpenAPI operation → contract test + handler implementation tasks (marked [P] when independent).
  - Entities in `data-model.md` → domain model + validator tasks preceding handler work.
  - Quickstart scenarios → integration test cases establishing TDD order.
  - Observability + resilience requirements → dedicated tasks for logging, metrics, retry policies, and alerting configuration.
- Ordering rules:
  1. Generate failing contract & validation tests.
  2. Build domain + application layers (validators → handlers → infrastructure).
  3. Wire telemetry and auditing.
  4. Finalize performance + compliance harness before enabling production transport.
- Expect roughly 26-30 tasks grouped by slice (search, versions, compatibility, observability, deployment).

## Phase 3+: Future Implementation
- **Phase 3** (`/tasks`): produce numbered execution plan with parallelizable tags.
- **Phase 4**: Implement vertical slices adhering to constitution, commit after tests green.
- **Phase 5**: Validate via full test matrix, run BenchmarkDotNet smoke, review telemetry dashboards, and prepare release artifacts with signatures.

## Complexity Tracking
No deviations identified; table intentionally left empty.

## Progress Tracking

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
