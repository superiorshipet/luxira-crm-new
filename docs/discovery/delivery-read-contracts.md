# Delivery read contracts

Status: Characterized and implemented as Phase 3 pilot reads  
Legacy source: `DataListController` at legacy commit `c345252`  
Verification boundary: Static legacy inspection and in-memory API tests only; no database connection was made

## Authentication

All routes below preserve the legacy `[Authorize]` requirement. The new API's fallback policy denies anonymous access unless an endpoint explicitly opts out.

## Visible delivery companies

| Contract | Method | Route |
|---|---|---|
| Canonical v1 | GET | `/api/v1/delivery-companies` |
| Legacy compatibility | GET | `/DataList/GetAllDeliveryCompanies` |

- Repeated `countryIds` query values are optional.
- The SQL predicate is `IsShown && !IsRepresentative` plus the country filter when supplied.
- The response remains `{ id, name, logoUrl }[]`.
- No ordering is added because the legacy query did not define one.

## Visible delivery representatives

| Contract | Method | Route |
|---|---|---|
| Canonical v1 | GET | `/api/v1/delivery-representatives` |
| Legacy compatibility | GET | `/DataList/GetAllDeliveryRepresentatives` |

- Repeated `countryIds` and `cityIds` query values are optional.
- The SQL predicate is `IsShown && IsRepresentative` plus supplied filters.
- A city filter containing only blank values is treated as no city filter, matching the legacy guard.
- If at least one nonblank city is supplied, the original collection is used for exact city matching.

## Delivery price

| Contract | Method | Route |
|---|---|---|
| Canonical v1 | GET | `/api/v1/delivery-companies/{deliveryCompanyId}/price` |
| Legacy compatibility | GET | `/DataList/GetDeliveryPrice` |

- `countryId` is required and `cityId` is optional.
- A matching city price wins over the country-wide row where `City` is null.
- With no `cityId`, null-city rows retain legacy precedence.
- A missing price returns HTTP 200 with `{ "price": 0 }`.

## Combined delivery options

| Contract | Method | Route |
|---|---|---|
| Canonical v1 | GET | `/api/v1/delivery-options` |
| Legacy compatibility | GET | `/DataList/GetAllDeliveryCompaniesAndRepresentatives` |

- The list includes visible non-representatives followed by visible representatives.
- `countryId` filters both groups; nonempty `cityId` filters representatives only.
- For Call Center users with `orderId`, the order's store assignment restricts the result to its single configured delivery company.
- A missing order/store assignment, manual-transfer assignment, or null assigned company returns an empty HTTP 200 array.
- For other roles, `orderId` is intentionally ignored, matching the legacy branch condition.

## Media URL compatibility

The shared feature-local resolver preserves `Common.NormalizeMediaUrl` behavior used by the legacy endpoints:

- null, empty, or whitespace becomes `/static/DefaultImage.svg`;
- values starting with `/`, `http://`, or `https://` are unchanged;
- all other values receive a leading `/`.

## Persistence and failure behavior

- EF Core mappings are query-only and use no tracking through the pooled read context.
- SQL retries are bounded to three and command timeout is 30 seconds.
- No response cache is enabled until mutation-driven invalidation is defined.
- Development and Testing can start without SQL; affected routes return RFC Problem Details with HTTP 503.
- Production and Staging fail startup when `ConnectionStrings:LuxiraSqlServer` is absent.
