<!--
Sync Impact Report
Version: 0.0.0 → 1.0.0
Modified Principles:
- (new) I. Security-First MCP Integration
- (new) II. Performance & Resilience Discipline
- (new) III. Observable Operations & Traceability
- (new) IV. Architectural Integrity & Modularity
- (new) V. Test & Compliance Driven Delivery
Added Sections:
- Platform Standards & Technology Baseline
- Development Workflow & Quality Gates
Removed Sections:
- None
Templates:
- ✅ .specify/templates/plan-template.md (Constitution Check gates aligned with v1.0.0)
- ✅ .specify/templates/spec-template.md (No changes required)
- ✅ .specify/templates/tasks-template.md (Sequencing reinforced for v1.0.0)
Follow-ups:
- None
-->

# Pub Dev MCP Constitution

## Core Principles

### I. Security-First MCP Integration
All MCP servers, tools, and transports MUST enforce defense-in-depth security every release:
- Enforce OAuth 2.1 with PKCE, strict `aud` validation, and short-lived tokens for every external connection.
- Apply schema-based JSON-RPC request validation, RBAC at the tool boundary, and rate limiting with prompt-injection defenses.
- Sign releases, pin dependencies, and monitor supply chain artifacts for tampering.
- Sign releases, pin dependencies, and monitor supply chain artifacts for tampering.

**Rationale**: Protecting user data, AI credentials, and tool access is table stakes for MCP adoption.

### II. Performance & Resilience Discipline
Every feature MUST honor the platform’s throughput and latency budgets while gracefully degrading:
- Use .NET 9, C# 13, `System.IO.Pipelines`, `Span<T>`, pooling (`ArrayPool<T>`, `ObjectPool<T>`), and `ValueTask` for high-throughput IO paths.
- Wrap remote and internal dependencies with Polly resilience policies (retry + jitter, circuit breakers, timeouts) and the Result/OneOf pattern.
- Surface centralized exception handling with `IExceptionHandler`, mapping to JSON-RPC errors predictably.

**Rationale**: MCP integrations run in critical workflows; performance and correctness keep assistants responsive.

### III. Observable Operations & Traceability
Runtime behavior MUST be transparent and diagnosable end-to-end:
- Emit structured, zero-allocation logs via Serilog with source-generated `LoggerMessage` for hot paths.
- Publish OpenTelemetry traces, metrics, and health endpoints compatible with Kubernetes probes and OTLP exporters.
- Capture long-lived session metrics, progress events, and JSON-RPC traffic metadata while respecting privacy rules.

**Rationale**: Observability shortens incident resolution and underpins reliability guarantees.

### IV. Architectural Integrity & Modularity
Codebases MUST combine Clean Architecture with Vertical Slice delivery:
- Organize features as CQRS slices using MediatR and enforce domain/application/infrastructure/presentation boundaries.
- Register MCP tools via `[McpServerTool]` attribute scanning and Microsoft.Extensions.DependencyInjection; no manual service locators.
- Apply DATAS, configuration via Options/IOptionsSnapshot, and maintain feature isolation for maintainability.

**Rationale**: Modularity preserves agility, enables selective deployment, and reduces coupling across tools.

### V. Test & Compliance Driven Delivery
Every change MUST prove correctness before merge:
- Use xUnit with theory cases, TestHost/WebApplicationFactory for transport checks, and JSON-RPC/WebSocket compliance suites.
- Mock external systems with Moq/NSubstitute, profile critical code with BenchmarkDotNet, and require failing tests before implementation.
- Automate quality gates: security scans, performance budgets, and compliance verification for OAuth, RBAC, and schema contracts.

**Rationale**: Comprehensive testing prevents regressions and enforces constitutional standards.

## Platform Standards & Technology Baseline
- Target C# 13 with .NET 9, enabling primary constructors, collection expressions, and modern `async/await` patterns.
- Standardize on `System.Threading.Lock` for fine-grained synchronization and prefer async, non-blocking flows.
- Default transport support MUST include stdio (local) and Streamable HTTP (production) with JSON-RPC 2.0 semantics.
- Deployment artifacts SHOULD ship as ReadyToRun or AOT builds with multi-stage Dockerfiles optimized for Azure Container Apps, AWS ECS/EKS, Google Cloud Run, or serverless hosts.
- Configuration MUST follow the Options pattern with hot-reload via `IOptionsSnapshot`, secret rotation, and environment-specific overrides.

## Development Workflow & Quality Gates
- Begin every feature with `/spec` → `/plan` → `/tasks`, ensuring constitution checks run before design and after technical planning.
- Enforce TDD: write contract, integration, and unit tests first; confirm they fail; then implement with smallest increments.
- Integrate security, observability, and performance acceptance criteria into Definition of Done for each task slice.
- Require code review sign-off on constitutional compliance, architecture boundaries, and telemetry coverage prior to merge.
- Maintain documentation: XML comments, Architecture Decision Records, and AsyncAPI/OpenAPI specs updated alongside code.

## Governance
- This constitution supersedes other process docs; conflicts must be resolved in favor of constitutional requirements.
- Amendments REQUIRE a written rationale, updated Sync Impact Report, semantic version bump, and alignment updates across `.specify/templates/*` within the same change set.
- Versioning: MAJOR when principles or governance rules change materially; MINOR when adding principles/sections; PATCH for clarifications.
- Ratification and amendments happen via reviewed pull requests; compliance reviews occur quarterly and before major releases.
- Violations MUST be logged in Progress Tracking and resolved or explicitly waived with stakeholder approval and follow-up tasks.

**Version**: 1.0.0 | **Ratified**: 2025-09-25 | **Last Amended**: 2025-09-25