# ADR 0004: API Authentication Direction

Status: Proposed  
Date: 2026-09-01

## Context

The legacy backend mixes long-lived Identity cookies, ten-day mobile JWTs, opaque machine tokens, webhook shared secrets, and an MVC-to-JSON Flutter bridge. The replacement must preserve client continuity while removing hidden behavior and improving credential rotation.

## Proposed decision

- Use explicit JSON authentication endpoints.
- Use JWT Bearer access tokens for Flutter/mobile.
- Use short-lived access tokens with rotating, revocable refresh tokens after a compatibility window.
- Store refresh tokens hashed with device/session metadata and reuse detection.
- Preserve a versioned `v1` login response compatible with Flutter during migration.
- Choose the web flow separately after confirming frontend hosting: prefer an HttpOnly-cookie BFF for a browser frontend when deployment topology permits it.
- Replace opaque global tokens with named, scoped machine clients.
- Implement provider-specific webhook authentication and replay protection.
- Express application authorization as named capability policies plus resource ownership handlers.
- Do not copy the `X-Flutter-App` MVC bridge.

## Compatibility mode

If existing Flutter releases cannot move immediately, the new API may temporarily validate legacy-issued JWTs using a dedicated compatibility scheme. New tokens should use a distinct issuer/signing configuration so compatibility can be removed cleanly.

Compatibility mode must have:

- an explicit removal date;
- telemetry identifying legacy-token usage;
- no extension of the legacy token format;
- no hardcoded tokens or credentials.

## Decision still required

Confirm the web frontend topology and the required Flutter upgrade window before changing this ADR to Accepted.

