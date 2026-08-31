# Luxira Backend Modernization Plan

Status: Approved  
Approved on: 2026-09-01  
Target: ASP.NET Core Web API on .NET 10  
Source system: `/media/superior/New Volume/luxira work/luxira-crm-main`  
New system: `/media/superior/New Volume/luxira work/luxira crm new`

## 1. Mission

Build a new backend that reproduces the business behavior of the legacy Luxira CRM while being faster, smoother under load, easier to understand, and easier to debug.

The project may be large when the business requires it. The goal is not the smallest codebase; the goal is the smallest amount of accidental complexity.

Priority order:

1. Preserve business behavior and data integrity.
2. Stability and predictable failure handling.
3. Low latency and high throughput.
4. Feature-local readability and debugging.
5. Minimal abstractions and dependencies.
6. Gradual, reversible migration.

## 2. Non-negotiable safety rules

- Never run experiments, tests, migrations, schema inspection, profiling, load testing, or ad-hoc queries against the production database.
- Production database access is out of scope unless the user grants separate, explicit approval for a specific read-only operation.
- Development and integration tests use a local database, an isolated test database, or an anonymized restored snapshot.
- The legacy checkout is read-only during the discovery and foundation phases.
- Never perform a big-bang replacement.
- Every migrated feature must have a rollback route or feature flag.
- Never change a public contract silently. Preserve it or introduce an explicitly versioned contract.
- Durable business data belongs in SQL/object storage, never only in process memory or Redis.
- Redis is an optimization and coordination dependency, not the source of truth.
- Database schema changes are deployed explicitly. Controllers, request handlers, and startup code must not create or alter tables at runtime.
- External side effects must be idempotent and observable.

## 3. Architecture decision

Use a modular monolith with feature-based vertical slices.

Initial deployable processes:

- `Luxira.Api`: stateless HTTP API and SignalR endpoints.
- `Luxira.Worker`: outbox consumers, scheduled jobs, long-running work, PDF generation, notifications, and integration retries.

Supporting projects:

- `Luxira.Domain`: business invariants, value objects, and state-transition rules.
- `Luxira.Infrastructure`: EF Core, Redis, S3, external providers, messaging, and observability implementations.
- `Luxira.Contracts`: stable API and integration message contracts where sharing is genuinely required.
- `Luxira.ServiceDefaults`: OpenTelemetry, health checks, and common host configuration.

Test projects:

- unit tests;
- integration tests;
- API contract tests;
- architecture tests;
- performance tests.

API verification assets:

- generated OpenAPI documents for every supported version;
- a Postman collection that covers every published operation;
- local and isolated-test Postman environments with no committed secrets;
- an automated collection runner and endpoint-coverage gate in CI.

Microservices are not an initial target. A module can become a service later only when measurements and operational ownership justify the network and consistency costs.

## 4. Feature slice convention

Each operation is kept close to its contract, validation, behavior, and tests.

```text
Features/Orders/UpdateStatus/
├── Endpoint.cs
├── Request.cs
├── Response.cs
├── Handler.cs
├── StatusTransitionPolicy.cs
└── Tests.cs
```

The normal execution path should remain visible:

```text
Endpoint -> Handler -> Domain rule -> DbContext or integration port
```

Files are added only when they carry real behavior. A slice does not need empty interfaces, validators, mappers, or services merely to match a template.

Default exclusions:

- no generic repository over EF Core;
- no MediatR by default;
- no AutoMapper in hot paths;
- no catch-all `Common`, `Helpers`, or `Services` dumping grounds;
- no reflection-heavy pipeline without a measured benefit;
- no distributed architecture before it is needed.

## 5. Business modules

The legacy controller layout does not define the new boundaries. The initial module map is:

1. Identity and Access.
2. Orders and Order Lifecycle.
3. Fulfillment, Warehouses, Preparation, and Packaging.
4. Delivery and Courier Integrations.
5. Finance, Transfers, Payroll, and Bonus.
6. Employees, Attendance, Shifts, and Access Rules.
7. Help Center and Internal Communication.
8. Media and File Storage.
9. Stores, Scripts, and Automation.
10. Notifications and Realtime.
11. Reporting, Invoices, and PDF Generation.
12. Administration, Diagnostics, and Observability.

Boundaries are validated during contract discovery. They may be refined before production implementation begins.

## 6. Integration architecture

Known integrations are first-class migration scope:

- CAMEX;
- Sandoog;
- Infobip SMS and WhatsApp;
- WhatsApp automation;
- Facebook webhooks;
- AWS S3;
- AWS CloudWatch;
- Cloudflare;
- SMTP and operational email;
- PDF and invoice generation;
- SignalR;
- voice transcription and image analysis;
- Flutter authentication and API compatibility.

Each provider adapter must have:

- a small provider-neutral port;
- typed configuration with startup validation;
- a typed `HttpClient` where HTTP is used;
- explicit timeout and cancellation propagation;
- bounded retry with exponential backoff and jitter;
- circuit breaking where appropriate;
- idempotency and provider correlation identifiers;
- webhook authentication and replay protection;
- structured logs, traces, and metrics;
- reconciliation for missing callbacks;
- a fake/test implementation;
- sanitized error mapping that preserves actionable provider failures.

Requests do not wait for avoidable external side effects. SQL state and an outbox record are committed together; `Luxira.Worker` performs the external action and records its outcome.

## 7. Data strategy

The new and legacy applications initially coexist over the current SQL Server schema.

- Keep schema changes backward compatible during coexistence.
- Keep one clearly owned migration chain and generate reviewed idempotent deployment scripts.
- Do not apply migrations automatically on application startup.
- Remove legacy runtime DDL before its feature is cut over.
- Use EF Core 10 for writes and ordinary queries.
- Use explicit DTO projections for reads.
- Use Dapper only for measured hot queries where it materially improves the result.
- Use `AsNoTracking` for read-only EF queries.
- Add optimistic concurrency to commands that can overwrite concurrent work.
- Use explicit transactions around business invariants.
- Use an outbox for reliable side effects.
- Use idempotency keys for retryable client commands and provider callbacks.
- Reconcile object storage writes whose SQL index write failed.

The initial context may cover the existing schema to reduce migration risk. Module-owned contexts can be introduced later when transactional boundaries are proven.

## 8. Orders migration rules

Orders are the highest-risk module because lifecycle rules are spread across controllers, services, integrations, background jobs, realtime messages, finance, and warehouse flows.

Before replacing an order command:

- document allowed and forbidden status transitions;
- document role and country conditions;
- document transaction boundaries;
- document history/audit rows;
- document notifications and integration side effects;
- document required images and evidence;
- document bulk-operation differences;
- add characterization and concurrency tests.

Status changes move to explicit domain policies. They must not remain scattered string/enum assignments across endpoints.

## 9. Redis and caching

Use Redis through `IDistributedCache` and .NET `HybridCache` where caching is safe.

Suitable candidates:

- reference data;
- provider tokens with safe expiry margins;
- expensive, stable read models;
- feature configuration;
- rate-limit and idempotency coordination;
- SignalR backplane when more than one API instance is used.

Rules:

- use versioned, namespaced keys;
- prevent cache stampedes;
- prefer event-driven invalidation;
- retain a bounded TTL as a recovery mechanism;
- record hit, miss, factory duration, and invalidation metrics;
- design multi-instance L1 invalidation explicitly;
- never cache sensitive responses without a proven key and authorization model;
- never use Redis as durable Help Center message or attachment storage;
- store attachments in S3 and durable references in SQL.

The new API should be stateless. Legacy session behavior must be translated into explicit request state, durable workflow state, tokens, or short-lived distributed coordination rather than copied blindly.

## 10. Background work

Move background work out of the web process.

- Transactional outbox for business events.
- Idempotent consumers.
- Bounded concurrency per provider and workload type.
- Persistent retry state.
- Dead-letter state with a safe replay operation.
- Distributed ownership for schedules.
- Graceful shutdown and cancellation.
- Health and backlog metrics.
- No controller-level fire-and-forget `Task.Run`.

An ADR will select the scheduler/job framework after comparing Quartz.NET persistent scheduling, Hangfire, and a small SQL-leased worker against actual requirements. The outbox remains independent of that selection.

## 11. Authentication and API contracts

- Replace the MVC-to-JSON Flutter bridge with explicit JSON endpoints.
- Preserve current Flutter behavior through a versioned `v1` compatibility contract where required.
- Preserve web and mobile claims, roles, and authorization rules.
- Decide web authentication through an ADR: secure HttpOnly cookie/BFF or access and rotating refresh tokens.
- Keep mobile JWT validation explicit.
- Remove browser Session as an API dependency.
- Use consistent RFC Problem Details errors.
- Publish OpenAPI for every supported API version.
- Add contract tests for status codes, JSON names, nullability, pagination, authorization, and errors.

## 12. Performance strategy

Performance work is measurement driven.

Measure for each hot endpoint:

- p50, p95, p99, and max latency;
- throughput and concurrent users;
- SQL query count and duration;
- external dependency duration;
- allocations, GC pressure, CPU, and memory;
- cache hit ratio and factory duration;
- error and timeout rate.

Default implementation rules:

- async I/O end to end;
- no `.Result`, `.Wait()`, or sync-over-async;
- project only required columns;
- mandatory bounded pagination for lists;
- eliminate N+1 queries;
- batch bulk operations;
- pool and reuse outbound connections;
- source-generated or explicit JSON contracts where useful;
- do not perform PDF generation or avoidable network calls on request threads;
- use compiled queries, Dapper, DbContext pooling, ReadyToRun, or PGO only after measuring the specific path.

Performance targets are set after collecting the legacy baseline. Every migrated hot endpoint must be no slower than the measured legacy endpoint under comparable load, with an agreed improvement target for the final cutover.

## 13. Observability and operations

- OpenTelemetry traces, metrics, and structured logs.
- Correlation and causation identifiers across HTTP, outbox, workers, SignalR, and providers.
- Redaction of credentials and personal data.
- Readiness and liveness checks.
- Dependency-specific health information without leaking secrets.
- Outbox age, queue depth, retry, and dead-letter dashboards.
- Provider latency and error-rate dashboards.
- Slow SQL visibility with safe parameter handling.
- Preserve useful existing Luxira profiling dimensions during migration.

## 14. Testing strategy

- Characterization tests capture current behavior before refactoring it.
- Unit tests cover domain rules and algorithms.
- Integration tests use isolated SQL Server, Redis, and storage substitutes/containers.
- Contract tests compare legacy and new API behavior.
- Concurrency tests cover duplicate requests and conflicting updates.
- Provider adapter tests cover retry, timeout, mapping, idempotency, and webhook replay.
- Golden-master tests protect invoices and PDFs where pixel-perfect compatibility is required.
- Load and soak tests run only against isolated environments.
- Architecture tests enforce module dependency rules.
- Postman provides executable end-to-end coverage for every published endpoint.
- Every endpoint has a stable OpenAPI `operationId` that maps to a Postman request.
- CI compares OpenAPI operations with the Postman coverage manifest and fails when an endpoint is missing.
- Postman suites include success, validation, authorization, forbidden-role, not-found, conflict/idempotency, and relevant concurrency scenarios.
- Test runs create uniquely named data and clean it up when safe; they never depend on production records.
- External providers use sandbox endpoints or controlled fakes during collection runs.

No test is allowed to point at the production database.

## 15. Migration phases and gates

### Phase 0: Characterization and inventory

- extract endpoints, routes, verbs, authorization, and DTOs;
- map business modules and cross-module dependencies;
- map database tables, writes, transactions, runtime DDL, and migrations;
- map integrations, webhooks, jobs, and side effects;
- capture sanitized legacy contract samples;
- establish performance baselines from an approved non-production environment;
- create an upgrade/package compatibility matrix.

Gate: no production feature implementation until the relevant legacy behavior is documented.

### Phase 1: Foundation

- create the .NET 10 solution and projects;
- enable central package management, nullable reference types, analyzers, and formatting;
- add API versioning, OpenAPI, Problem Details, health endpoints, configuration validation, and CI;
- add Postman collection generation, local/test environments, role-aware authentication setup, and the all-endpoints coverage gate;
- establish module and slice conventions with architecture tests.

Gate: clean foundation with no legacy business logic copied into shared dumping grounds.

### Phase 2: Infrastructure

- EF Core 10 SQL Server access;
- Redis and HybridCache;
- S3/media boundary;
- typed integration clients and resilience;
- OpenTelemetry;
- outbox and Worker foundation;
- secrets and environment configuration.

Gate: infrastructure integration tests pass against isolated dependencies.

### Phase 3: Low-risk pilot slices

- reference data;
- selected delivery-company reads;
- search keyword administration;
- media URL resolution;
- current user/profile.

Gate: contract, authorization, observability, and performance patterns proven end to end.

### Phase 4: Identity and compatibility

- web/mobile authentication contracts;
- role and permission policies;
- Flutter `v1` compatibility;
- remove implicit MVC-to-JSON behavior from migrated features.

Gate: existing clients can use migrated endpoints without silent behavior changes.

### Phase 5: Orders and fulfillment

Migrate in small waves:

1. reads, search, filters, and counts;
2. create order;
3. edit and inline edit;
4. status transition engine;
5. bulk status operations;
6. bank transfers;
7. follow-up and operational reporting;
8. warehouses, preparation, and packaging;
9. realtime and side effects.

Gate per wave: characterization, contract, integration, concurrency, and rollback checks pass.

### Phase 6: Integrations and background jobs

- CAMEX and Sandoog;
- communications providers;
- webhooks;
- media workflows;
- PDF/invoice worker;
- scheduled workflows;
- SignalR scale-out.

Gate: retries, idempotency, reconciliation, and dead-letter recovery are demonstrated.

### Phase 7: Performance hardening

- execution-plan and index review;
- hot-query optimization;
- caching and invalidation validation;
- load, soak, concurrency, and failure testing;
- memory and allocation profiling;
- capacity and scaling model.

Gate: agreed latency, throughput, error-rate, and stability targets are met.

### Phase 8: Gradual cutover

- route one feature at a time;
- shadow and compare safe reads;
- enable canary users/stores;
- increase traffic gradually;
- reconcile data and side effects;
- keep per-module rollback;
- retire legacy paths only after an agreed stable period.

## 16. Definition of done for a migrated feature

A feature is migrated only when:

- business behavior is documented and preserved;
- API behavior is compatible or versioned;
- authorization is preserved;
- data ownership and transaction boundaries are explicit;
- external side effects are reliable and idempotent;
- tests cover success, failure, validation, authorization, and concurrency;
- its endpoint operations and required role scenarios are present in the Postman suite;
- traces, logs, and metrics exist;
- measured performance meets its budget;
- rollback is documented and verified;
- the legacy path can be disabled without hidden consumers.

## 17. Immediate execution order

1. Generate a repeatable legacy endpoint inventory.
2. Produce the module/integration/business-risk catalog.
3. Document the Orders lifecycle and side effects first.
4. Create ADRs for authentication, job scheduling, database coexistence, and API style.
5. Scaffold the .NET 10 foundation only after the first discovery gate is reviewed.
