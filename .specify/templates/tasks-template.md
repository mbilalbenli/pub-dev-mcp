# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`
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
- **Web app**: `backend/src/`, `frontend/src/`
- **Mobile**: `api/src/`, `ios/src/` or `android/src/`
- Paths shown below assume single project - adjust based on plan.md structure

## Phase 3.1: Setup
- [ ] T001 Create project structure per implementation plan
- [ ] T002 Initialize [language] project with [framework] dependencies
- [ ] T003 [P] Configure linting and formatting tools

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**
- [ ] T004 [P] JSON-RPC contract test suite covering request/response schemas in `tests/contract/jsonrpc_contract_tests.cs`
- [ ] T005 [P] Transport compliance tests using `WebApplicationFactory` in `tests/transport/streamable_http_tests.cs`
- [ ] T006 [P] Security flow integration test validating OAuth 2.1 + PKCE in `tests/integration/security_flow_tests.cs`
- [ ] T007 [P] Resilience policy tests simulating transient failures in `tests/integration/resilience_policy_tests.cs`
- [ ] T008 Observability contract tests asserting Serilog + OpenTelemetry enrichment in `tests/integration/observability_tests.cs`

## Phase 3.3: Core Implementation (ONLY after tests are failing)
- [ ] T009 [P] Implement feature slice handlers with MediatR in `src/Application/[Feature]/Handlers`
- [ ] T010 [P] Wire domain models and persistence abstractions in `src/Domain` and `src/Infrastructure`
- [ ] T011 Materialize MCP tool command with `[McpServerTool]` attribute in `src/Presentation/Tools`
- [ ] T012 Configure JSON-RPC dispatcher and schema validators in `src/Presentation/JsonRpc`
- [ ] T013 Harden error mapping via `IExceptionHandler` + Result pattern in `src/Infrastructure/ErrorHandling`

## Phase 3.4: Integration
- [ ] T014 Provision OAuth 2.1 + PKCE authorization flow and token validation middleware
- [ ] T015 Configure RBAC, rate limiting, and prompt-injection guards per tool scope
- [ ] T016 Integrate Polly resilience pipelines (retry with jitter, circuit breaker, timeout) for outbound clients
- [ ] T017 Instrument Serilog sinks and OpenTelemetry exporters (traces, metrics, logs)
- [ ] T018 Expose readiness/liveness health checks aligned with deployment platform

## Phase 3.5: Polish
- [ ] T019 [P] Span/Memory optimization review with BenchmarkDotNet report in `benchmarks/`
- [ ] T020 Performance regression tests meeting latency/throughput budgets
- [ ] T021 [P] Update telemetry + security documentation in `docs/observability.md` and `docs/security.md`
- [ ] T022 Verify configuration hot-reload and Options snapshots across environments
- [ ] T023 Run manual specification validation via `/spec` → `/plan` → `/tasks` workflow dry-run

## Dependencies
- Tests (T004-T008) before implementation (T009-T018)
- T009 and T010 unblock T011-T013
- T014 requires T011-T013
- T016 requires T015
- Implementation before polish (T019-T023)

## Parallel Example
```
# Launch T004-T007 together:
Task: "JSON-RPC contract test suite covering request/response schemas"
Task: "Transport compliance tests using WebApplicationFactory"
Task: "Security flow integration test validating OAuth 2.1 + PKCE"
Task: "Resilience policy tests simulating transient failures"
```

## Notes
- [P] tasks = different files, no dependencies
- Verify tests fail before implementing
- Commit after each task
- Avoid: vague tasks, same file conflicts

## Task Generation Rules
*Applied during main() execution*

1. **From Contracts**:
   - Each contract file → contract test task [P]
   - Each endpoint → implementation task
   
2. **From Data Model**:
   - Each entity → model creation task [P]
   - Relationships → service layer tasks
   
3. **From User Stories**:
   - Each story → integration test [P]
   - Quickstart scenarios → validation tasks

4. **Ordering**:
   - Setup → Tests → Models → Services → Endpoints → Polish
   - Dependencies block parallel execution

## Validation Checklist
*GATE: Checked by main() before returning*

- [ ] All contracts have corresponding tests
- [ ] All entities have model tasks
- [ ] All tests come before implementation
- [ ] Parallel tasks truly independent
- [ ] Each task specifies exact file path
- [ ] No task modifies same file as another [P] task