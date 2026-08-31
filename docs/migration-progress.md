# Backend migration progress

Updated: 2026-09-01  
Plan: `BACKEND_MODERNIZATION_PLAN.md`  
Production database access: Never performed

## Phase status

| Phase | Status | Evidence / remaining gate |
|---|---|---|
| 0. Characterization and inventory | In progress | System, integrations, authentication, Orders lifecycle, and initial database ownership are documented. Full endpoint/table-write catalog and non-production performance baseline remain. |
| 1. Foundation | In progress | .NET 10 API, OpenAPI import URL with Bearer metadata, Default-Deny JWT authentication, Problem Details, health checks, response compression, safe output caching, central packages, OpenTelemetry service defaults, in-memory API integration/architecture tests, Postman coverage gate, and CI are present. Login/refresh-token persistence and remaining deployable projects remain. |
| 2. Infrastructure | In progress | Query-only SQL Server infrastructure, optional Redis/HybridCache wiring, isolated local containers, and metadata-only mapping tests are present. No database connection has been made; additional adapters and isolated runtime verification remain. |
| 3. Low-risk pilot slices | In progress | Countries, country cities, failure reasons, authenticated order sources, role-scoped order statuses, and the first SQL-backed delivery-company read contract are implemented with canonical and legacy-compatible routes and executable contract checks. |
| 4-8 | Not started | These phases stay gated by their prerequisite characterization and isolated tests. |

## Completed pilot evidence

- Legacy conventional route confirmed as `/DataList/GetAllCountries`.
- IDs, Arabic names, ordering, and image paths copied from the legacy enum/mapping without database access.
- Versioned route: `/api/v1/reference-data/countries`.
- Legacy-compatible route retained for current JavaScript consumers.
- Both route pairs are present in generated OpenAPI and the curated Postman suite.
- Local gate verifies all published operations plus exact public reference-data contracts; integration tests cover JWT and role matrices for protected contracts.
- Delivery-company tests replace the SQL reader in-memory, preserving database isolation while exercising filtering and legacy media URL normalization.
- Build passes with zero warnings and zero errors.

## Next safe execution order

1. Complete the remaining read-only delivery/reference contracts one operation at a time.
2. Prove SQL Server/Redis failure behavior against isolated local dependencies only.
3. Add `Domain`, `Contracts`, and `Worker` projects only when their first real consumer requires them; do not create empty abstractions.
4. Introduce storage/integration adapters behind explicit ports with timeouts, retries, and observability.
5. Characterize Orders writes before migrating any command path.
