# ADR 0002: Production Data Safety

Status: Accepted  
Date: 2026-09-01

## Decision

The modernization project must not use the production database for experimentation or verification.

Prohibited without a new, explicit, operation-specific approval:

- opening a production SQL connection;
- running read or write queries;
- applying or testing migrations;
- capturing execution plans;
- load, soak, or concurrency testing;
- seeding, repairing, or reconciling production rows;
- using production as an integration-test dependency.

Allowed environments are a local database, a dedicated isolated test database, or an anonymized restored snapshot. Connection configuration for tests must fail closed when the environment is ambiguous.

Production deployment will eventually use reviewed migration scripts and controlled rollout procedures, but that is not authorization to access production during development.

