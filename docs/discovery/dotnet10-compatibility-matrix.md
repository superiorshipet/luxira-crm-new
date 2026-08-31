# .NET 10 package and runtime compatibility matrix

Status: Working baseline  
Checked: 2026-09-01  
Rule: Stable releases only; every infrastructure package requires isolated integration tests before production use

| Capability | Selected / candidate version | Status | Decision notes |
|---|---:|---|---|
| .NET SDK/runtime | `10.0.111` / `10.0.11` | Active | Pinned through `global.json`; patch roll-forward only. |
| ASP.NET Core OpenAPI | `10.0.11` | Active | Generates the Postman-importable document and security metadata. |
| ASP.NET Core JWT Bearer | `10.0.11` | Active | Compatible with .NET 10 and the legacy HMAC JWT settings contract. |
| ASP.NET Core MVC Testing | `10.0.11` | Active | In-memory API contract tests; no network port or database. |
| Microsoft.NET.Test.Sdk | `18.9.0` | Active | Current stable test SDK verified with the .NET 10 test project. |
| xUnit / VS adapter | `2.9.3` / `4.0.0` | Active | Stable v2 framework with the current adapter; 15 tests currently pass. |
| EF Core SQL Server | `10.0.11` | Approved candidate | Microsoft provider targets .NET 10. Must be tested against an isolated SQL Server schema snapshot before registration. All Microsoft EF packages stay on exactly the same patch. |
| Redis distributed cache | `Microsoft.Extensions.Caching.StackExchangeRedis 10.0.11` | Approved candidate | Preferred `IDistributedCache` backend. Redis remains optional acceleration/coordination, never source of truth. |
| HybridCache | `Microsoft.Extensions.Caching.Hybrid 10.9.0` | Approved candidate | Adds stampede protection over memory plus `IDistributedCache`. Introduce only on SQL/provider reads with explicit key versioning and invalidation. |
| StackExchange.Redis direct API | `3.1.31` | Deferred | Use only for primitives not covered by `IDistributedCache`/HybridCache, such as atomic coordination. Avoid exposing it to feature handlers by default. |
| OpenTelemetry hosting/instrumentation/exporter | `1.18.0` | Active | ASP.NET Core and HttpClient traces/metrics. OTLP export is disabled unless an explicit endpoint is configured. |
| SQL migrations tooling | EF Core Design `10.0.11` | Approved candidate | Design-time only/private asset. No startup migration application. |
| S3 SDK | Not pinned yet | Pending adapter spike | Pin after verifying streaming, cancellation, checksum, retry, and LocalStack/S3-compatible integration tests. |
| PDF/native stack | Not pinned yet | High-risk research | Legacy DinkToPdf/Pdfium/System.Drawing behavior needs isolated Linux runtime and golden-master validation. |
| Scheduler | Not selected | ADR required | Compare persistent Quartz, Hangfire, and SQL-leased worker against actual schedules and retry ownership. |

Official package evidence:

- [EF Core SQL Server 10.0.11](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/)
- [Redis distributed cache 10.0.11](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis/)
- [HybridCache 10.9.0](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Hybrid)
- [OpenTelemetry hosting 1.18.0](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/)
- [OpenTelemetry ASP.NET Core instrumentation 1.18.0](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/)
- [OpenTelemetry OTLP exporter 1.18.0](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/)

## Safety gate

Adding a package to this matrix does not authorize a production connection. SQL Server, Redis, object storage, provider, load, and migration tests run only against explicitly isolated dependencies. Production connection strings are never used by automated tests or local tools.
