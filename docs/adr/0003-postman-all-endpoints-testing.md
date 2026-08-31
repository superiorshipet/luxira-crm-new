# ADR 0003: Postman Coverage for Every API Endpoint

Status: Accepted  
Date: 2026-09-01

## Context

The new backend must make every endpoint easy to exercise manually and automatically. A hand-maintained Postman collection alone will drift as endpoints are added or renamed.

## Decision

OpenAPI is the inventory of published endpoints. Every operation has a stable and unique `operationId`. Postman is the executable end-to-end layer built from that inventory, with curated assertions added for business behavior and authorization.

CI must compare the current OpenAPI operations with the Postman coverage manifest. A published operation without Postman coverage fails the pipeline.

## Test layers

1. Generated request coverage for every OpenAPI operation.
2. Curated smoke tests for application availability and authentication.
3. Feature regression folders for business behavior.
4. Role-based authorization scenarios.
5. Negative validation and error-contract scenarios.
6. Idempotency, conflict, and concurrency scenarios where applicable.
7. Provider sandbox/fake scenarios for external integrations.

## Environment rules

- Commit only variable names and safe local defaults.
- Never commit passwords, API keys, refresh tokens, cookies, or provider credentials.
- Provide separate Local and isolated Test environments.
- Reject a run when `baseUrl` or database-safety metadata indicates Production.
- Generate a unique `runId` for test records.
- Prefer API-driven setup and teardown over direct SQL.
- A Postman run must never connect to the production database.

## Collection convention

```text
00 Platform/
01 Authentication/
02 Identity and Access/
03 Orders/
04 Fulfillment/
05 Delivery Integrations/
06 Finance/
07 Workforce/
08 Help Center/
09 Media/
10 Store Automation/
11 Notifications and Realtime/
12 Reporting/
13 Administration/
99 Cleanup/
```

Each request stores its OpenAPI `operationId` in the request description or coverage manifest. Feature folders may contain additional requests for alternative roles and failure scenarios without changing the one-operation coverage rule.

## Consequences

- Developers can run one feature folder while debugging.
- QA can run the whole API without learning internal implementation details.
- New endpoints cannot silently escape end-to-end coverage.
- OpenAPI remains accurate because tooling depends on it.
- Business assertions still require human design; generation only guarantees request inventory coverage.

