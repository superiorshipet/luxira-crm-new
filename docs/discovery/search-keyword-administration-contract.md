# Search keyword administration contract

Status: Characterized for the first read-only migration slice  
Legacy source: `HomeController.SearchKeywords.cs` at legacy commit `c345252`  
Verification boundary: Static inspection and in-memory tests only; no database connection was made

## List operation

| Contract | Method | Route | Authorization |
|---|---|---|---|
| Canonical v1 | GET | `/api/v1/administration/search-keywords` | Admin only |
| Legacy compatibility | GET | `/Home/GetSearchKeywords` | Admin only |

Optional filters are `search`, `targetType`, `category`, and `isActive`.

- `search` is trimmed and matched against Phrase, DisplayLabel, Category, or TargetValue.
- blank `targetType`/`category` and the exact value `All` mean no filter.
- results are ordered by IsActive descending, then Id descending.
- success remains `{ ok: true, keywords: [...] }`.
- every keyword contains Id, Phrase, NormalizedPhrase, TargetType, TargetValue, DisplayLabel, Category, IsActive, IsSingleResult, CreatedAt/By, and UpdatedAt/By.
- the legacy route preserves its HTTP 200 `{ ok: false, error }` failure envelope; the canonical route uses HTTP 503 Problem Details for unavailable read infrastructure.

## Schema boundary

The legacy controller creates/alters/seeds `HomeSearchKeywords` at runtime. The new API must never execute that DDL. The EF mapping is query-only and assumes a reviewed migration has provisioned the table in an approved non-production environment before runtime SQL verification.

## Editor options

| Contract | Method | Route | Authorization |
|---|---|---|---|
| Canonical v1 | GET | `/api/v1/administration/search-keywords/options` | Admin only |
| Legacy compatibility | GET | `/Home/GetSearchKeywordOptions` | Admin only |

Categories are distinct, nonempty SQL values ordered ascending. If the read store is unavailable, the exact seven legacy fallback categories are returned. Target types and their option arrays are immutable legacy catalogs; their declaration order and the duplicate Facebook source entry are intentionally preserved.

## Deferred commands

Save, toggle, single delete, and bulk delete remain deferred until their validation, normalization, transaction, audit timestamps, duplicate handling, and cache invalidation contracts are fully characterized. They are not silently approximated by this read slice.
