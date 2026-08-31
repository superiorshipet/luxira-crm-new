# Legacy database ownership and transaction map

Status: Static discovery baseline  
Source: Legacy source code only  
Production database access: Not performed and not permitted

## Purpose

This map identifies the initial data owners and the highest-risk transaction boundaries before EF Core is introduced into the new API. It is a migration map, not a claim that the legacy code already enforces these boundaries.

The legacy `ApplicationDbContext` exposes 104 application `DbSet` properties in addition to ASP.NET Identity tables. Controllers and services currently cross those sets freely, so ownership below is the target ownership for the modular monolith while the physical SQL schema remains shared during coexistence.

## Initial logical ownership

| Owning module | Principal tables / entity sets | Known cross-module consumers | Migration notes |
|---|---|---|---|
| Identity and Access | Identity users, roles, claims, `Employees`, `UserSwitchGroups`, `UserSwitchGroupMembers` | Almost every module | Identity owns credentials and claims. Workforce owns employee operational details; commands spanning both require an explicit application transaction. |
| Orders | `Orders`, `OrderEditHistories`, `OrderStatusHistories`, `OrderUserChangeHistories`, `OrderFromCommentsHistories`, `OrderStatusUpdateSelections`, `StatusUpdateBatchLogs`, `StatusUpdateBatchLogItems`, `OrderInvestigationApprovals`, `OrderInvestigationOpenings` | Fulfillment, Delivery, Finance, Reporting, Notifications | Highest-risk aggregate. Status and audit rows must commit atomically; external notifications belong in the same commit as an outbox row. |
| Fulfillment | `Warehouses`, `MainWarehouses`, `SubWarehouses`, `OrderWarehouses`, `OrderWarehouseEditHistories`, preparation and packaging session/assignment tables | Orders, Delivery, Workforce | Reserve/prepare/package commands must define concurrency and stock invariants before cutover. |
| Delivery | `DeliveryCompanies`, `DeliveryCompanyPrices`, `StoreDeliveryCompanyAssignments`, `CamexCities`, `CamexCityMappings`, `CamexStoreMappings` | Orders, Fulfillment, Integrations | Provider requests happen after commit through outbox consumers; mappings remain durable SQL data. |
| Finance | `EmployeeTransactions`, `EmployeeSalaryPayments`, `EmployeePaymentSummaries`, `EmployeeBonusRates`, `EmployeeBonusPayments`, `Expenses`, `ExchangeRates`, `CountryMinimumPrices`, `ProductMinimumSellingPrices`, `OrderBonusConfigurations` | Orders, Workforce, Reporting | Money writes need explicit transaction and idempotency boundaries; decimal precision must be configured explicitly. |
| Workforce | `Employees`, `EmployeeTasks`, `EmployeeTaskAssignments`, `EmployeeWorkShifts`, `EmployeeAttendanceLogs`, `EmployeeActivityLogs`, `EmployeeActivityHourlyLogs`, `EmployeeErrors`, `EmployeeErrorEditHistories` | Identity, Orders, Notifications | `Employees` is a shared legacy hotspot. New writes must go through Workforce-owned operations instead of direct cross-module mutation. |
| Help Center and Communication | Help Center messages/order links, `SocialMediaConversations`, `SocialMediaMessages`, `PotentialOrders`, `Leads` | Orders, Media, Notifications | Messages and attachment references are durable. Redis cannot be their source of truth. |
| Media | `ProductImages`, `ProductImageDrafts`, `ProductImageUserPins`, `S3StoredObjects`, `MediaReferenceCleanupRuns`, `MediaReferenceCleanupSettings`, `EmployeeScreenRecords`, `CallRecordings` | Orders, Stores, Help Center | SQL owns metadata/reference state; object storage owns bytes. Reconciliation covers one-sided failures. |
| Stores and Automation | `StoreCodeFolders`, `StoreCodeEditHistories`, `StoreCodeStoreGroups`, `AdvertisingManagerStoreFolders`, `AdvertisingManagerItems`, script definition/target/country/category/message/translation sets, WhatsApp automation account/template/log sets | Orders, Integrations, Media | Configuration changes invalidate cache by version/event. Send logs are durable and idempotent. |
| Administration and Diagnostics | `AppLogs`, `AppMetrics`, `ClientLogs`, `SystemEmailLogs`, `WebsiteDomains`, `WebsiteDomainEditLogs`, `PasswordPageTypes`, `StorePasswordPages`, `PasswordPageChangeLogs` | All modules | Operational tables must not become synchronous hot-path bottlenecks; secrets and personal data are redacted. |
| Reporting | `OrderReports`, `OrderReportOrders`, `SalesIndicators` and generated invoice/read models | Orders, Finance | Prefer projections/read models. Reporting does not mutate the Orders aggregate directly. |

## Transaction hotspots found statically

Explicit transaction calls appear in at least 21 legacy controllers/services, including Orders, Employees, Delivery Companies, Help Center, Stores, Warehouses, development tasks, and background status transitions. This proves that a single generic transaction policy would hide important differences.

For each migrated command, the characterization document must record:

1. rows read for decisions;
2. rows inserted, updated, and deleted;
3. concurrency expectation;
4. audit/history rows;
5. external and realtime side effects;
6. commit point and rollback behavior;
7. idempotency key or duplicate-detection rule.

The minimum Orders boundary is `Orders` plus its relevant status/edit/user/warehouse history and outbox records. SignalR, provider calls, email, WhatsApp, PDF work, and object-storage work happen only after that SQL commit, with reconciliation where storage cannot share the SQL transaction.

## Runtime DDL hazards

Static scanning found runtime `CREATE TABLE` or `ALTER TABLE` statements in 21 controller/service files. High-risk examples include `OrderController`, `EmployeeController`, `EmployeeErrorsController`, `OrderPreparationWorkflowService`, and `OrderPackagingWorkflowService`.

New-system rule:

- endpoint, handler, service, worker startup, and request paths never create or alter schema;
- every required schema change is represented by a reviewed migration and deployment script;
- legacy runtime DDL for a feature is removed only when that feature is cut over and the deployed schema is confirmed in an approved non-production environment first.

## Pilot decision

`DataList/GetAllCountries` is the first slice because its source is a compile-time enum and image mapping. It performs no SQL reads/writes, has no transaction or integration side effects, and has a small exact response contract. Both its legacy route and a versioned canonical route can therefore be characterized without database access.

Redis is intentionally excluded from this slice. The data already lives in-process, is tiny and immutable for the lifetime of the application, so a remote cache would add latency and a failure mode. Redis/HybridCache will be introduced for reference data only when the source is SQL or an external provider and invalidation semantics are known.
