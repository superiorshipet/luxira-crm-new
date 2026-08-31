# Legacy Authentication and Authorization Contract

Status: Phase 0 working document  
Captured: 2026-09-01  
Method: static source inspection only

No authentication request or database connection was performed.

## 1. Existing authentication modes

The legacy application has three materially different caller types:

1. Browser users authenticated with ASP.NET Core Identity cookies.
2. Flutter/mobile users authenticated with JWT Bearer tokens.
3. Machine/provider callers authenticated with opaque accepted tokens or webhook shared secrets.

The ASP.NET Core default scheme is a `Smart` policy scheme:

- a request containing `Authorization: Bearer ...` is forwarded to JWT Bearer;
- other requests are forwarded to the Identity application cookie.

The new API must model these callers explicitly. It must not preserve scheme selection through hidden MVC behavior.

## 2. Mobile login contract

Legacy endpoint:

```text
POST /Api/Auth/Login
```

Input fields:

- `email`;
- `password`.

Observed response on success:

```json
{
  "token": "<jwt>",
  "expiresInDays": 10
}
```

Observed failures:

- missing email/password: `400` with a plain-string message;
- invalid user/password: `401` with `{ "error": "Invalid credentials." }`.

The JWT:

- uses HMAC-SHA256;
- expires after ten days;
- validates issuer, audience, lifetime, and signing key;
- includes `sub` as username;
- includes `jti` as user ID;
- includes `NameIdentifier` as user ID;
- includes Identity user claims and role claims.

The new `v1` compatibility endpoint must preserve the response shape until Flutter is migrated. A later version should use short-lived access tokens and rotating refresh tokens.

## 3. Forgot-password contract

Legacy endpoint:

```text
POST /Api/Auth/ForgotPassword
```

It intentionally returns success when the user does not exist to avoid account enumeration. It creates an Identity reset token and sends an email containing the reset-page link.

The behavior must remain enumeration-safe. Email delivery belongs in an outbox/worker; the endpoint records the request without exposing whether the account exists.

## 4. Browser cookie behavior

- Identity application cookie name is `.AspNetCore.Identity.Application`.
- Cookie is HttpOnly, essential, SameSite Lax, and SameAsRequest for secure policy.
- Sliding expiration is enabled.
- The configured lifetime is ten years to approximate "until explicit logout".
- Security-stamp validation runs every minute.
- Identity requires confirmed accounts.

The ten-year cookie must not be copied automatically. The web-client architecture must decide between a BFF/HttpOnly-cookie flow and an explicit access/refresh-token flow.

## 5. Flutter MVC bridge

Legacy Flutter behavior relies partly on `X-Flutter-App: true`:

- cookie redirects are converted to JSON `401`/`403` responses;
- a global action filter converts some MVC `ViewResult` results into JSON;
- some existing MVC actions therefore behave as undocumented mobile APIs.

This bridge is not copied. Every migrated endpoint returns an explicit, documented JSON contract and appears in OpenAPI/Postman.

## 6. Machine tokens and webhooks

`TokenAuthorizeAttribute` accepts opaque bearer values from an `AcceptedTokens` configuration list. It has no observed scopes, caller identity model, expiry, or rotation metadata. A missing configuration section can also result in a server error rather than a clean unauthorized response.

At least one legacy path couples an opaque token value to business attribution in source code. The value is deliberately not reproduced in this document. This must be removed rather than migrated.

Sandoog callbacks use a `Secret-Key` header, fail closed when configuration is absent, and compare the configured shared secret in fixed time.

The new design uses named machine clients with:

- a client identifier;
- hashed/managed credentials;
- explicit scopes;
- expiry and rotation;
- rate limits;
- audit records;
- fixed-time validation;
- no credential-dependent business identity hardcoded in source.

Provider webhooks additionally require replay protection and idempotency. Use a provider signature/timestamp when available; otherwise combine a rotated shared secret with payload deduplication and strict network/rate controls.

## 7. Observed roles

- Accountant
- Admin
- CallCenter
- DeliveryCompany
- DeliveryRepresentative
- ExecutiveDirector
- FollowUpDepartment
- MarketingDepartment
- Observer
- OrderPreparer
- SoftwareDeveloper
- TeamLeader
- Team Leader
- WareHouse

`TeamLeader` and `Team Leader` are both present. The new system needs one canonical permission model with a temporary alias mapping during migration.

Role checks are frequently combined with ownership, country, store, delivery-company, workflow, or employee-state checks. Replacing an attribute with a role-only policy would be insufficient.

## 8. New authorization shape

- Default deny: endpoints require authentication unless explicitly public.
- Stable named policies represent business capabilities, not copied comma-separated role lists.
- Resource authorization handlers enforce order/store/delivery ownership.
- Role aliases are normalized at the authentication boundary.
- Policy decisions are unit tested as matrices.
- `401` means missing/invalid authentication.
- `403` means authenticated but not permitted.
- Public tracking/media contracts receive dedicated narrow policies and rate limits.
- CORS is explicit per deployed frontend/mobile need; `AllowAnyOrigin` is not the production default.

## 9. Postman authentication support

The isolated test fixture will create test identities for each canonical permission group. The Postman setup folder will:

1. authenticate test identities through API endpoints;
2. store access tokens in environment current values;
3. run positive and forbidden-role cases;
4. avoid committed credentials;
5. reject Production environments.

Machine-client and webhook folders use sandbox credentials supplied through secret variables.

## 10. Open questions

- Will the new web frontend use the same site origin, a separate SPA origin, or a BFF?
- Must the first new API deployment validate already-issued legacy Flutter JWTs?
- What mobile release window is available for moving from ten-day JWTs to refresh-token rotation?
- Which opaque accepted tokens still have active external consumers?
- Can provider clients migrate to scoped credentials without simultaneous downtime?
- Which role spelling is canonical in current operational data?

