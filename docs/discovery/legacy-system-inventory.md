# Legacy Backend Inventory

Status: Phase 0 working document  
Source snapshot: legacy branch `main`, commit `c345252`  
Captured: 2026-09-01  
Method: static source inspection only

No build, runtime request, database connection, or production verification was performed for this inventory.

## Repository shape

| Area | Observed size |
|---|---:|
| ASP.NET projects | 1 |
| Target framework | `net6.0` |
| Controllers | 104 files |
| Services | 92 C# files |
| Models | 218 C# files |
| Razor views | 278 files |
| EF migration files | 431 files |
| C# lines excluding generated migrations | about 225,000 |
| Razor lines | about 458,000 |
| HTTP verb attributes | about 782 |
| Hosted-service registrations | about 19 |
| SignalR hubs | 5 |

There is no automated test project or checked-in CI workflow in the inspected snapshot.

## Largest risk concentrations

| Area | Static evidence | Risk |
|---|---|---|
| Orders | `OrderController.cs` is about 1 MB and more than 23,000 lines | Critical |
| Order lifecycle | About 480 status assignments/comparisons across many files | Critical |
| UI/server coupling | MVC views plus a Flutter result-conversion filter | High |
| Background execution | Many hosted services share the web process | High |
| Schema ownership | Controllers/services contain runtime `CREATE`/`ALTER` SQL | Critical |
| Caching | `IMemoryCache` is common; no application use of `IDistributedCache` was found | High for scale-out |
| Session | Around 90 Session references | High for stateless API migration |
| External side effects | Email, courier, messaging, media, and realtime calls cross request flows | Critical |
| Async behavior | `.Result`, `.Wait`, controller `Task.Run`, and direct `HttpClient` creation exist | High |
| Native reporting | DinkToPdf, PdfiumViewer, jsreport, and System.Drawing are used | High |

## Existing strengths to preserve

- Extensive use of `AsNoTracking` already exists in read paths.
- The database model defines many workload-specific indexes.
- The application has custom p50/p95/p99 and slow-SQL diagnostics.
- Several integrations already contain retry/reconciliation concepts.
- S3 media indexing and cleanup concepts already exist.
- Some large domains, such as `HomeController`, have begun using partial files.
- Bonus caching has explicit event-driven invalidation logic.

These are behaviors to carry forward selectively, not files to copy wholesale.

## API compatibility concerns

- Conventional MVC routes coexist with attribute-routed APIs.
- Only a small subset of controllers use `[ApiController]` explicitly.
- Flutter depends partly on `X-Flutter-App` and a global filter that converts MVC results to JSON.
- Authentication selects JWT when a Bearer header exists and otherwise uses the Identity cookie.
- Error responses may therefore differ by endpoint, authentication mechanism, and request headers.
- Existing web pages may call conventional controller routes directly rather than a documented API contract.

The new API requires an explicit contract catalog covering routes, verbs, request sources, JSON shape, status codes, authorization, and side effects.

## Database concerns

- A single large `ApplicationDbContext` owns Identity and business tables.
- Runtime schema checks and DDL are present in order workflows and multiple controllers.
- EF, raw SQL, and database transactions are mixed.
- Some schema compatibility logic is executed on startup.
- The application performs both synchronous and asynchronous database work.
- Background services construct scopes and access the same database independently.

The replacement must first coexist with the current schema. Runtime DDL moves to reviewed migrations before each affected feature is cut over.

## Public-source exposure candidate

The tracked file below is under the static web root and contains a large C# copy of order-controller code:

```text
wwwroot/css/home/index/inline-v2/OrderController.cs
```

Because `UseStaticFiles()` is enabled, this is a potential source-disclosure issue. It requires a separate urgent remediation in the legacy application, with deployment verification, without waiting for the rewrite to finish.

## Initial module boundaries

| Module | Representative legacy areas |
|---|---|
| Identity and Access | Identity pages, account switching, roles, employee access |
| Orders | Order, Home order slices, status history, follow-up, ratings |
| Fulfillment | Warehouses, preparation, packaging, pending downloads |
| Delivery | Delivery companies, representatives, CAMEX, Sandoog |
| Finance | Financial, transfers, invoices, expenses, bonus, payroll |
| Workforce | Employees, attendance, shifts, tasks, activity, errors |
| Help Center | Chat, keywords, message-order links, inactive reads |
| Media | S3, images, screen records, recordings, cleanup |
| Store Automation | Store codes, store scripts, WhatsApp automation |
| Communication | Email, SMS, WhatsApp, Facebook, notifications |
| Realtime | Order, message, conference, edit-presence, code-editor hubs |
| Reporting | PDFs, daily invoices, operational reports |
| Platform | Diagnostics, CloudWatch, Cloudflare, logging, profiling |

## Discovery work still required

- Produce a complete endpoint and authorization catalog.
- Resolve DTO and response shapes for Flutter and browser consumers.
- Map every table written by each command.
- Build the exact order-status transition matrix.
- Identify all post-commit and fire-and-forget side effects.
- Map SignalR event names and consumer pages/apps.
- Identify provider idempotency keys and webhook replay behavior.
- Build the NuGet/native-runtime compatibility matrix for .NET 10.
- Establish performance baselines in an approved non-production environment.

