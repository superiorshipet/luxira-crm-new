# Luxira API Postman Suite

The API itself publishes an OpenAPI document containing every registered endpoint. This is the primary automatic Postman import mechanism.

For Local development, start the API and import this link in Postman:

```text
http://localhost:5100/swagger/v1/swagger.json
```

The HTTP local URL avoids untrusted-development-certificate errors in Postman. HTTPS is also available at `https://localhost:7100/swagger/v1/swagger.json` after trusting the local .NET development certificate.

Use **Import -> Link**, paste the URL, and select the option that generates a Postman Collection. ASP.NET Core adds every mapped endpoint, HTTP method, parameter, request schema, response schema, tag, and security description to this document automatically.

Opening `http://localhost:5100/` returns a small discovery response containing the current `openApiUrl`.

Every Minimal API endpoint must use `.WithName("Module_Operation")`; ASP.NET Core uses that endpoint name as the OpenAPI `operationId`. `.WithTags("Module")` controls the generated Postman folders.

The checked-in collection in this directory is the executable regression suite. It complements the automatically generated collection with authentication flows, business assertions, negative cases, setup, and cleanup.

## Safety

The collection is for Local and isolated Test environments only. It must not be run against Production or against an application configured with the production database.

The collection-level pre-request guard stops execution when:

- `environmentKind` is not `Local` or `Test`;
- `allowDestructiveTests` is not explicitly `true` for scenarios that create or delete data;
- the base URL matches a known production host.

Do not store credentials or tokens in exported environment files. Use Postman's local/current values or CI secret variables.

## Required variables

| Variable | Purpose |
|---|---|
| `baseUrl` | API origin, without a trailing slash |
| `environmentKind` | `Local` or `Test` |
| `allowDestructiveTests` | Explicit guard for isolated data-changing tests |
| `runId` | Unique identifier generated for each full run |
| `adminAccessToken` | Admin bearer token supplied locally/through CI |
| `callCenterAccessToken` | Call-center bearer token |
| `followUpAccessToken` | Follow-up bearer token |
| `deliveryAccessToken` | Delivery-company/representative bearer token |
| `employeeAccessToken` | Ordinary authenticated-user token |

Additional role tokens will be added when the authorization inventory is complete.

## Coverage model

Every OpenAPI operation must have:

1. a stable `operationId`;
2. a matching entry in `coverage-manifest.json`;
3. at least one Postman request;
4. status, content type, and common error-contract assertions;
5. feature-specific assertions where behavior is more than simple retrieval.

The coverage checker will compare the generated OpenAPI document with the manifest and fail on missing, duplicate, or stale operation IDs.

Add this marker to the description of the primary Postman request for an operation:

```text
operationId: Orders_CreateOrder
```

Then add the same value to `coverage-manifest.json`. Alternative role and failure requests do not need additional manifest entries.

The dependency-free coverage checker is:

```text
node tools/check-postman-coverage.mjs <openapi.json> postman/coverage-manifest.json postman/Luxira.Api.postman_collection.json
```

To start the already-built API temporarily, fetch its live OpenAPI document, verify full Postman coverage, and stop it automatically:

```text
./tools/verify-openapi-postman.sh
```

The verifier uses the `Testing` environment, an isolated localhost port, and no database configuration.

## Running locally

Runner commands will be added with the .NET 10 foundation so they use the checked-in tool version. The intended workflow is:

```text
start isolated dependencies
start Luxira.Api in Testing environment
seed test identities through the test fixture
run the full Postman collection
publish JUnit/JSON results
tear down isolated dependencies
```

No direct SQL setup is required from Postman.
