# Backend migration progress

Updated: 2026-09-04

Plan: `BACKEND_MODERNIZATION_PLAN.md`

Legacy snapshot compared: `../luxira-crm-main` at `c713deb`

Production database access: never performed

## Current status

The backend migration and legacy compatibility implementation are complete for the checked legacy snapshot, including CAMEX and Sandoog. The remaining gates are deployment/environment checks, not missing in-repository logic.

| Gate | Result |
|---|---|
| Legacy routes | 928 candidates, 928 exact matches, 0 missing; CAMEX/Sandoog included |
| Authorization | 628 comparable protected actions, 0 differences |
| Build | 0 warnings, 0 errors |
| Automated tests | 282 passed, 0 failed, 0 skipped |
| Development SQL schema | 142 mapped tables; 0 missing tables/columns, nullability issues, or type mismatches; 142 read-only SELECT checks passed |
| Authenticated API smoke | 380 canonical GETs and 4 SignalR negotiations; 0 server failures and 0 timeouts |
| API latency | p50 96.6 ms, p95 565.1 ms, total 72.4 s for the 380-route sequential smoke |
| OpenAPI/Postman | 2,330 published operations; 2,323 generated requests; coverage and reference-data parity gates passed |
| Working-tree integrity | `git diff --check` passed |

## Final compatibility fixes

- Replaced the incorrect trainee-store implementation that mutated `StoreCodeFolders` with the legacy `TraineeStores` plus `TraineeStoreManufacturingCompanies` many-to-many workflow.
- Preserved legacy form endpoints while keeping JSON endpoints for the versioned API.
- Added mapped trainee entities, indexes, guarded additive migration, validation, error contracts, and regression tests.
- Removed unrestricted bulk store-code content loading. The list endpoint now returns metadata; content remains behind the dedicated access-checked endpoint.
- Restored legacy warehouse and main-warehouse `Index` pagination/filter behavior using server-side SQL pagination.
- Kept modern warehouse collection contracts and reduced materialization by projecting only response fields.
- Completed S3/media migration, cleanup auditing, background upload/cleanup, restricted serving, and orphan/reference protection.
- Completed courier parity and deterministic CAMEX/Sandoog HTTP contract tests.

## Performance evidence

The initial authenticated new-backend smoke took 108.7 seconds with three 15-second timeouts. The final run took 72.4 seconds with no timeouts: about 33% less total time. The legacy warehouse implementation materializes the full filtered result before in-memory pagination; the new compatibility endpoint performs count, filter, order, skip, take, and projection in SQL. The legacy trainee page executes schema-existence DDL on each request; the new endpoint uses pre-mapped tables and indexed queries.

This proves improvement inside the new backend and removes known legacy query costs. It is not a direct old-process versus new-process benchmark because the legacy MVC application was not started under the same authenticated workload.

## Environment-only gates

- Run real CAMEX and Sandoog sandbox/production calls with provider credentials and approved test shipments.
- Run real S3 upload/delete/migration against the intended bucket with approved disposable objects.
- Run deployment smoke in the target hosting environment.
- A fresh `git fetch` of the legacy remote needs GitHub credentials; the comparison used the locally available `origin/main` snapshot at `c713deb`.
