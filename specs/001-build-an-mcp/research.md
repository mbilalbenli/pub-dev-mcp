# Phase 0 Research: Pub.dev Package Intelligence MCP

## Decision Log

### Decision: Adopt .NET 9 with vertical-slice Clean Architecture for the MCP server
- **Rationale**: Aligns with the constitution requirement for C# 13/.NET 9, enables `System.IO.Pipelines`, `Span<T>`, and native `ValueTask` support for high-throughput message handling while keeping vertical slice boundaries clear.
- **Alternatives considered**: Older .NET LTS (8.0) dismissed because it would violate the constitution and miss C# 13 language improvements.

### Decision: Map each MCP tool to specific pub.dev API endpoints
- **Rationale**: Ensures deterministic data retrieval and keeps implementation loosely coupled to the public API surface. Search uses `GET /api/search?q=`, package metadata uses `GET /api/packages/{package}`, version list uses `GET /api/packages/{package}/versions`, scores use `GET /api/packages/{package}/score`, and dependency trees use `GET /api/packages/{package}/versions/{version}` (for pubspec parsing).
- **Alternatives considered**: Scraping HTML pages was rejected because it is brittle, higher latency, and risks breaching pub.dev usage policy.

### Decision: Perform compatibility analysis by evaluating Flutter SDK constraints from pubspec metadata
- **Rationale**: pub.dev publishes `environment.sdk` constraints inside each version’s pubspec. Evaluating those constraints against the requested Flutter SDK gives authoritative compatibility answers.
- **Alternatives considered**: Guessing compatibility based on release date or heuristic scoring would be inaccurate and unverifiable.

### Decision: Implement resilience using Polly policies (retry + circuit breaker + timeout)
- **Rationale**: Constitution mandates Polly resilience. Combining exponential backoff retries (max 3), a circuit breaker for repeated failures, and request timeouts ensures graceful degradation while honoring FR-005.
- **Alternatives considered**: Manual retry loops or relying solely on `HttpClient` defaults provide poorer observability and no centralized resilience configuration.

### Decision: Emit structured telemetry via Serilog and OpenTelemetry exporters
- **Rationale**: Constitution requires Serilog with source generators plus OpenTelemetry traces/metrics. Integrating both provides consistent diagnostics across MCP deployments and supports health probes.
- **Alternatives considered**: Lightweight logging (Console.WriteLine) rejected due to lack of structure, poor performance, and non-compliance.

### Decision: Validate inputs with source-generated validators before network calls
- **Rationale**: FR-007 demands strict validation. Using FluentValidation with source generators (or custom analyzers) ensures format checking for package names, semantic versions, and caret ranges before hitting pub.dev.
- **Alternatives considered**: Relying on downstream API validation would leak errors directly to users and complicate UX.

### Decision: Cache score insights and dependency graphs for short TTLs
- **Rationale**: Scores and dependency data change infrequently. Using `IMemoryCache` with 10-minute TTL reduces load on pub.dev and improves perceived latency while staying within retention policies.
- **Alternatives considered**: No caching would increase latency and risk rate-limit responses for repeated queries.
