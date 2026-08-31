# Countries reference-data contract

Status: Characterized and implemented as the first pilot slice  
Legacy source: `DataListController.GetAllCountries` and `Common.Countries`  
Verification environment: Local only; no database is used

## Routes

| Purpose | Method | Route | OpenAPI operation ID |
|---|---|---|---|
| Canonical v1 | GET | `/api/v1/reference-data/countries` | `ReferenceData_GetCountries` |
| Legacy compatibility | GET | `/DataList/GetAllCountries` | `LegacyDataList_GetAllCountries` |
| Preparation canonical v1 | GET | `/api/v1/reference-data/countries/preparation-for-delivery` | `ReferenceData_GetPreparationForDeliveryCountries` |
| Preparation legacy compatibility | GET | `/DataList/GetPfdCountries` | `LegacyDataList_GetPfdCountries` |

The legacy application uses conventional `{controller}/{action}/{id?}` routing, and existing JavaScript calls `/DataList/GetAllCountries`. The compatibility route is therefore retained exactly. New clients should use the versioned route.

Both routes are anonymous, matching the legacy action, which has no authorization attribute or global authenticated-user fallback policy.

## Response

Success status: `200 OK`  
Media type: JSON  
Shape: array ordered by the legacy enum numeric value

```json
[
  {
    "id": 1,
    "name": "العراق",
    "imageUrl": "/Countries/iraq.svg"
  }
]
```

The complete catalog contains 16 entries with stable IDs `1..16`. Names preserve the exact enum spelling, including `سلطنة_عمان`, because clients may persist or compare these values. Image URLs preserve their legacy relative paths and casing.

The preparation-for-delivery routes return the exact legacy subset and order: IDs `[1, 4, 5, 2]` (Iraq, Libya, Oman, UAE). This intentionally does not use numeric or alphabetical sorting.

## Behavior and ownership

- Source data is compile-time configuration, not SQL.
- No request input, validation branch, transaction, cache invalidation, integration, or side effect exists.
- The response is generated without allocation-heavy reflection on every request.
- Redis is deliberately not used: this catalog is already in process and immutable, so a remote lookup would be slower and less reliable.

## Compatibility and rollback

- The local verification gate asserts 16 entries, the first and last exact objects, the four-entry preparation subset/order, and byte-for-byte parity for each canonical/legacy route pair.
- The curated Postman suite exercises both operation IDs.
- OpenAPI generated from the running application publishes both operations.
- Rollback is route-level: the legacy application continues owning production traffic until explicit cutover approval.
