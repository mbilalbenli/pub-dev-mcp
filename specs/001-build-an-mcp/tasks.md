# Tasks: Pub.dev Package Intelligence MCP

**Input**: Design documents from `specs/001-build-an-mcp/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → If not found: ERROR "No implementation plan found"
   → Extract: tech stack, libraries, structure
2. Load optional design documents:
   → data-model.md: Extract entities → model tasks
   → contracts/: Each file → contract test task
   → research.md: Extract decisions → setup tasks
3. Generate tasks by category:
   → Setup: project init, dependencies, linting
   → Tests: contract, transport, security, resilience, observability
   → Core: feature slices, JSON-RPC pipeline, error handling
   → Integration: security boundaries, resilience policies, telemetry, health
   → Polish: performance optimization, documentation, workflow validation
4. Apply task rules:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → All contracts have tests?
   → All entities have models?
   → All endpoints implemented?
9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **Single project**: `src/`, `tests/` at repository root
- Paths shown below assume single project with Clean Architecture structure from plan.md

## Phase 3.1: Setup
- [ ] T001 Create .NET 9 solution and project structure per implementation plan at `src/PubDevMcp.Server/`, `src/PubDevMcp.Application/`, `src/PubDevMcp.Domain/`, `src/PubDevMcp.Infrastructure/` and `tests/contract/`, `tests/integration/`, `tests/compliance/`, `tests/performance/`
- [ ] T002 Initialize .NET 9 projects with NuGet dependencies: MediatR, Polly, Serilog, OpenTelemetry, FluentValidation, System.Text.Json, BenchmarkDotNet, xUnit, FluentAssertions in appropriate project files
- [ ] T003 [P] Configure EditorConfig, Directory.Build.props, and global.json for C# 13/.NET 9 conventions at repository root

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**
- [ ] T004 [P] Create OpenAPI contract validation tests for all 8 MCP tools in `tests/contract/OpenApiContractTests.cs` using Approval testing to verify schema compliance
- [ ] T005 [P] Create JSON-RPC 2.0 compliance test suite covering request/response validation and error mapping in `tests/compliance/JsonRpcComplianceTests.cs`
- [ ] T006 [P] Create MCP transport tests for stdio and HTTP modes using TestHost in `tests/integration/McpTransportTests.cs`
- [ ] T007 [P] Create search packages contract test validating 10-result limit and pagination hints in `tests/contract/SearchPackagesContractTests.cs`
- [ ] T008 [P] Create latest version contract test for stable version retrieval in `tests/contract/LatestVersionContractTests.cs`
- [ ] T009 [P] Create compatibility check contract test for Flutter SDK constraint evaluation in `tests/contract/CheckCompatibilityContractTests.cs`
- [ ] T010 [P] Create version list contract test including prerelease filtering in `tests/contract/ListVersionsContractTests.cs`
- [ ] T011 [P] Create package details contract test for metadata retrieval in `tests/contract/PackageDetailsContractTests.cs`
- [ ] T012 [P] Create publisher packages contract test for publisher-scoped search in `tests/contract/PublisherPackagesContractTests.cs`
- [ ] T013 [P] Create score insights contract test for component score breakdown in `tests/contract/ScoreInsightsContractTests.cs`
- [ ] T014 [P] Create dependency inspector contract test for full dependency tree generation in `tests/contract/DependencyInspectorContractTests.cs`
- [ ] T015 [P] Create resilience policy integration tests simulating pub.dev failures and retry scenarios in `tests/integration/ResiliencePolicyTests.cs`
- [ ] T016 [P] Create observability integration tests validating Serilog structured logging and OpenTelemetry traces in `tests/integration/ObservabilityTests.cs`

## Phase 3.3: Core Domain Models (ONLY after tests are failing)
- [ ] T017 [P] Implement PackageSummary domain entity in `src/PubDevMcp.Domain/Entities/PackageSummary.cs`
- [ ] T018 [P] Implement VersionDetail domain entity in `src/PubDevMcp.Domain/Entities/VersionDetail.cs`
- [ ] T019 [P] Implement SearchResultSet domain entity in `src/PubDevMcp.Domain/Entities/SearchResultSet.cs`
- [ ] T020 [P] Implement CompatibilityRequest and CompatibilityResult domain entities in `src/PubDevMcp.Domain/Entities/Compatibility.cs`
- [ ] T021 [P] Implement DependencyNode and DependencyGraph domain entities in `src/PubDevMcp.Domain/Entities/DependencyGraph.cs`
- [ ] T022 [P] Implement ScoreInsight domain entity in `src/PubDevMcp.Domain/Entities/ScoreInsight.cs`
- [ ] T023 [P] Implement AuditLogEntry domain entity in `src/PubDevMcp.Domain/Entities/AuditLogEntry.cs`

## Phase 3.4: Application Layer (Vertical Slices)
- [ ] T024 [P] Create SearchPackages MediatR handler with pub.dev API integration in `src/PubDevMcp.Application/Features/SearchPackages/SearchPackagesHandler.cs`
- [ ] T025 [P] Create LatestVersion MediatR handler with pub.dev API integration in `src/PubDevMcp.Application/Features/LatestVersion/LatestVersionHandler.cs`
- [ ] T026 [P] Create CheckCompatibility MediatR handler with SDK constraint evaluation in `src/PubDevMcp.Application/Features/CheckCompatibility/CheckCompatibilityHandler.cs`
- [ ] T027 [P] Create ListVersions MediatR handler with prerelease filtering in `src/PubDevMcp.Application/Features/ListVersions/ListVersionsHandler.cs`
- [ ] T028 [P] Create PackageDetails MediatR handler with metadata enrichment in `src/PubDevMcp.Application/Features/PackageDetails/PackageDetailsHandler.cs`
- [ ] T029 [P] Create PublisherPackages MediatR handler with publisher-scoped queries in `src/PubDevMcp.Application/Features/PublisherPackages/PublisherPackagesHandler.cs`
- [ ] T030 [P] Create ScoreInsights MediatR handler with component score analysis in `src/PubDevMcp.Application/Features/ScoreInsights/ScoreInsightsHandler.cs`
- [ ] T031 [P] Create DependencyInspector MediatR handler with dependency tree traversal in `src/PubDevMcp.Application/Features/DependencyInspector/DependencyInspectorHandler.cs`
- [ ] T032 [P] Create FluentValidation validators for all MCP tool requests in `src/PubDevMcp.Application/Validators/`
- [ ] T033 Create centralized pub.dev HTTP client service with base URL configuration in `src/PubDevMcp.Infrastructure/Services/PubDevApiClient.cs`

## Phase 3.5: Infrastructure & Resilience
- [ ] T034 Configure Polly resilience policies (retry with exponential backoff, circuit breaker, timeout) for pub.dev calls in `src/PubDevMcp.Infrastructure/Policies/PubDevResiliencePolicies.cs`
- [ ] T035 Implement structured audit logging service with request/response hashing in `src/PubDevMcp.Infrastructure/Services/AuditLoggingService.cs`
- [ ] T036 Configure IMemoryCache for score insights and dependency graph caching with 10-minute TTL in `src/PubDevMcp.Infrastructure/Services/CacheService.cs`
- [ ] T037 Implement centralized exception handler mapping to JSON-RPC Problem Details in `src/PubDevMcp.Infrastructure/ErrorHandling/GlobalExceptionHandler.cs`
- [ ] T038 Configure Serilog with source-generated LoggerMessage and structured JSON output in `src/PubDevMcp.Infrastructure/Logging/LoggingConfiguration.cs`
- [ ] T039 Configure OpenTelemetry traces, metrics, and activity sources for MCP request tracking in `src/PubDevMcp.Infrastructure/Telemetry/TelemetryConfiguration.cs`

## Phase 3.6: Presentation Layer
- [ ] T040 Create MCP tool attributes and registration for all 8 tools in `src/PubDevMcp.Server/Tools/McpTools.cs`
- [ ] T041 Implement JSON-RPC 2.0 request/response pipeline with schema validation in `src/PubDevMcp.Server/JsonRpc/JsonRpcPipeline.cs`
- [ ] T042 Configure dependency injection container with all services, policies, and Options pattern in `src/PubDevMcp.Server/Configuration/ServiceConfiguration.cs`
- [ ] T043 Implement stdio transport for local MCP client connections in `src/PubDevMcp.Server/Transports/StdioTransport.cs`
- [ ] T044 Implement HTTP transport for containerized deployments with health checks in `src/PubDevMcp.Server/Transports/HttpTransport.cs`
- [ ] T045 Create Program.cs with host builder, configuration, and transport selection logic in `src/PubDevMcp.Server/Program.cs`

## Phase 3.7: Integration & Validation
- [ ] T046 Wire health check endpoints (`/health/live`, `/health/ready`) with pub.dev connectivity validation in `src/PubDevMcp.Server/HealthChecks/PubDevHealthCheck.cs`
- [ ] T047 Create BenchmarkDotNet performance harness validating ≤7s p95 response times in `tests/performance/McpPerformanceBenchmarks.cs`
- [ ] T048 [P] Run all contract tests and verify they now pass after implementation
- [ ] T049 [P] Run integration test suite validating end-to-end MCP tool invocation scenarios
- [ ] T050 [P] Execute performance benchmarks and validate NFR-001 compliance (≤5s typical, ≤7s p95)
- [ ] T051 Create Docker multi-stage build optimized for .NET 9 AOT deployment in `Dockerfile`
- [ ] T052 Update quickstart.md documentation with final build and run instructions

## Dependencies
- Setup (T001-T003) before everything else
- Tests (T004-T016) before implementation (T017-T045)
- Domain models (T017-T023) before application handlers (T024-T032)
- Infrastructure services (T033-T039) before presentation layer (T040-T045)
- Core implementation (T017-T045) before integration validation (T046-T052)
- T033 (HTTP client) required for T024-T031 (handlers)
- T034 (resilience policies) required for T033 (HTTP client)

## Parallel Examples
```
# Launch contract tests together (Phase 3.2):
Task: "Create search packages contract test validating 10-result limit"
Task: "Create latest version contract test for stable version retrieval"
Task: "Create compatibility check contract test for Flutter SDK constraint evaluation"

# Launch domain models together (Phase 3.3):
Task: "Implement PackageSummary domain entity"
Task: "Implement VersionDetail domain entity"
Task: "Implement SearchResultSet domain entity"

# Launch application handlers together (Phase 3.4):
Task: "Create SearchPackages MediatR handler with pub.dev API integration"
Task: "Create LatestVersion MediatR handler with pub.dev API integration"
Task: "Create CheckCompatibility MediatR handler with SDK constraint evaluation"
```

## Notes
- [P] tasks target different files and have no dependencies
- All tests must fail initially (TDD requirement)
- Commit after each task completion
- Constitution compliance verified through security, resilience, observability, and architecture task coverage

## Task Generation Rules Applied
1. **From Contracts**: pubdev-mcp-openapi.yaml → 8 contract test tasks + 8 implementation tasks
2. **From Data Model**: 10 entities → 7 grouped model creation tasks  
3. **From Plan Architecture**: Clean Architecture layers → infrastructure, application, presentation tasks
4. **From Constitution**: Security, resilience, observability → dedicated infrastructure tasks
5. **From Quickstart**: Test scenarios → contract and integration test tasks

## Validation Checklist
- [x] All 8 MCP tools have contract tests
- [x] All 10 domain entities have model tasks  
- [x] All tests come before implementation (TDD)
- [x] Parallel tasks target independent files
- [x] Each task specifies exact file path
- [x] Constitution requirements covered (security, resilience, observability, architecture)
- [x] Performance validation included (BenchmarkDotNet)