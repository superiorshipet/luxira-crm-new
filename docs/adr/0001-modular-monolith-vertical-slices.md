# ADR 0001: Modular Monolith with Vertical Slices

Status: Accepted  
Date: 2026-09-01

## Context

The legacy Luxira backend combines MVC, JSON bridging, SignalR, SQL access, background services, file storage, reporting, and multiple external providers in one large application. Business rules, especially order lifecycle rules, cross controllers and services.

The replacement must preserve behavior while improving latency, stability, readability, and feature-level debugging. A big-bang rewrite or an early microservice split would add distributed failure modes before the business boundaries have been proven.

## Decision

Build a .NET 10 modular monolith. Organize use cases as feature-based vertical slices. Deploy the HTTP API and background worker as separate processes.

Use module boundaries for business ownership and slice boundaries for individual operations. Keep the common execution path explicit:

```text
Controller -> Application service -> Domain rule -> Repository/integration port
```

Use explicit feature repository ports for persisted use cases, implemented in Infrastructure. Keep transport controllers in API and orchestration services in Application. Do not introduce a generic repository, mediator, or mapping framework; each additional abstraction must carry real behavior.

## Consequences

- A bug should be traceable inside one feature directory in the normal case.
- Cross-module business rules must be explicit rather than hidden in shared helpers.
- Transactions remain local and understandable during the high-risk migration period.
- Modules can be extracted later if load or ownership proves that a separate service is worthwhile.
- Architecture tests are required to prevent feature and infrastructure boundaries from eroding.
