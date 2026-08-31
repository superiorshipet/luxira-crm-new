# Backend migration progress

Updated: 2026-09-01  
Plan: `BACKEND_MODERNIZATION_PLAN.md`  
Production database access: Never performed

## Phase status

| Phase | Status | Evidence / remaining gate |
|---|---|---|
| 0. Characterization and inventory | In progress | System, integrations, authentication, Orders lifecycle, and initial database ownership are documented. Full endpoint/table-write catalog and non-production performance baseline remain. |
| 1. Foundation | In progress | .NET 10 API, OpenAPI import URL, Problem Details, health checks, central packages, Postman coverage gate, and CI are present. Authentication implementation, architecture tests, and remaining deployable projects remain. |
| 2. Infrastructure | Not started | Requires approved isolated SQL Server/Redis/storage test dependencies; production is prohibited. |
| 3. Low-risk pilot slices | In progress | Countries reference data is implemented with canonical and legacy-compatible routes and executable contract checks. |
| 4-8 | Not started | These phases stay gated by their prerequisite characterization and isolated tests. |

## Completed pilot evidence

- Legacy conventional route confirmed as `/DataList/GetAllCountries`.
- IDs, Arabic names, ordering, and image paths copied from the legacy enum/mapping without database access.
- Versioned route: `/api/v1/reference-data/countries`.
- Legacy-compatible route retained for current JavaScript consumers.
- Both route pairs are present in generated OpenAPI and the curated Postman suite.
- Local gate verifies 7/7 published operations plus exact country contracts and route parity.
- Build passes with zero warnings and zero errors.

## Next safe execution order

1. Add the remaining small, DB-free reference-data contracts one operation at a time.
2. Complete the package/native-runtime compatibility matrix for infrastructure choices.
3. Scaffold `Domain`, `Infrastructure`, `Contracts`, `Worker`, and tests only when the first consumer requires them; do not create empty abstraction projects.
4. Introduce SQL Server/Redis/S3 adapters against isolated dependencies and prove their failure behavior.
5. Migrate selected read-only delivery data before any Orders write path.
