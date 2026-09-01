#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verification_port="${LUXIRA_OPENAPI_TEST_PORT:-5187}"
verification_configuration="${LUXIRA_BUILD_CONFIGURATION:-Debug}"
base_url="http://127.0.0.1:${verification_port}"
temporary_directory="$(mktemp -d)"
server_log="$temporary_directory/server.log"
openapi_document="$temporary_directory/openapi-v1.json"
postman_document="$temporary_directory/postman-collection.json"
countries_document="$temporary_directory/countries.json"
legacy_countries_document="$temporary_directory/legacy-countries.json"
preparation_countries_document="$temporary_directory/preparation-countries.json"
legacy_preparation_countries_document="$temporary_directory/legacy-preparation-countries.json"
failure_reasons_document="$temporary_directory/failure-reasons.json"
legacy_failure_reasons_document="$temporary_directory/legacy-failure-reasons.json"
cities_document="$temporary_directory/cities.json"
legacy_cities_document="$temporary_directory/legacy-cities.json"
server_pid=""

cleanup() {
    if [[ -n "$server_pid" ]] && kill -0 "$server_pid" 2>/dev/null; then
        kill "$server_pid" 2>/dev/null || true
        wait "$server_pid" 2>/dev/null || true
    fi

    rm -rf "$temporary_directory"
}

trap cleanup EXIT INT TERM

cd "$repository_root"

ASPNETCORE_ENVIRONMENT=Testing \
ASPNETCORE_URLS="$base_url" \
dotnet run \
    --project Luxira.csproj \
    --no-build \
    --configuration "$verification_configuration" \
    --no-launch-profile \
    >"$server_log" 2>&1 &

server_pid=$!

for _ in $(seq 1 80); do
    if curl --fail --silent --show-error \
        "$base_url/health/live" >/dev/null 2>&1; then
        break
    fi

    if ! kill -0 "$server_pid" 2>/dev/null; then
        echo "Luxira.Api stopped before becoming ready." >&2
        sed -n '1,240p' "$server_log" >&2
        exit 1
    fi

    sleep 0.25
done

curl --fail --silent --show-error \
    "$base_url/swagger/v1/swagger.json" \
    --output "$openapi_document"

curl --fail --silent --show-error \
    "$base_url/postman/collection.json" \
    --output "$postman_document"

curl --fail --silent --show-error \
    "$base_url/api/v1/reference-data/countries" \
    --output "$countries_document"

curl --fail --silent --show-error \
    "$base_url/DataList/GetAllCountries" \
    --output "$legacy_countries_document"

curl --fail --silent --show-error \
    "$base_url/api/v1/reference-data/countries/preparation-for-delivery" \
    --output "$preparation_countries_document"

curl --fail --silent --show-error \
    "$base_url/DataList/GetPfdCountries" \
    --output "$legacy_preparation_countries_document"

curl --fail --silent --show-error \
    "$base_url/api/v1/reference-data/failure-reasons" \
    --output "$failure_reasons_document"

curl --fail --silent --show-error \
    "$base_url/DataList/GetAllFailureReasons" \
    --output "$legacy_failure_reasons_document"

curl --fail --silent --show-error \
    "$base_url/api/v1/reference-data/cities?countryIds=5&countryIds=1" \
    --output "$cities_document"

curl --fail --silent --show-error \
    "$base_url/DataList/GetCitiesByCountry?countryIds=5&countryIds=1" \
    --output "$legacy_cities_document"

jq --exit-status '
    length == 16 and
    .[0] == {"id": 1, "name": "العراق", "imageUrl": "/Countries/iraq.svg"} and
    .[15] == {"id": 16, "name": "مصر", "imageUrl": "/Countries/egypt.svg"}
' "$countries_document" >/dev/null

if ! cmp --silent "$countries_document" "$legacy_countries_document"; then
    echo "The versioned and legacy country contracts do not match." >&2
    exit 1
fi

jq --exit-status '
    length == 4 and
    map(.id) == [1, 4, 5, 2]
' "$preparation_countries_document" >/dev/null

if ! cmp --silent \
    "$preparation_countries_document" \
    "$legacy_preparation_countries_document"; then
    echo "The versioned and legacy preparation country contracts do not match." >&2
    exit 1
fi

jq --exit-status '
    .[0] == "مسقط" and
    .[-1] == "بعقوبة" and
    length == (unique | length)
' "$cities_document" >/dev/null

if ! cmp --silent "$cities_document" "$legacy_cities_document"; then
    echo "The versioned and legacy city contracts do not match." >&2
    exit 1
fi

jq --exit-status '
    length == 11 and
    .[8] == {"id": 9, "name": "تأجيل الاستلام"} and
    .[10] == {"id": 11, "name": "الطلب غير مطابق للمطلوب"}
' "$failure_reasons_document" >/dev/null

if ! cmp --silent \
    "$failure_reasons_document" \
    "$legacy_failure_reasons_document"; then
    echo "The versioned and legacy failure-reason contracts do not match." >&2
    exit 1
fi

operation_count="$(
    jq '[.paths[] | to_entries[] | select(.value.operationId != null)] | length' \
        "$openapi_document"
)"

postman_request_count="$(
    jq '[.. | objects | select(has("request"))] | length' \
        "$postman_document"
)"

jq --exit-status '
    .info.schema == "https://schema.getpostman.com/json/collection/v2.1.0/collection.json" and
    (.item | length) > 0
' "$postman_document" >/dev/null

echo "Verified Postman import document at $base_url/swagger/v1/swagger.json"
echo "Published operations: $operation_count"
echo "Generated Postman requests: $postman_request_count"
echo "Verified country contract and legacy-route parity: 16 entries"
echo "Verified preparation country contract and legacy-route parity: 4 entries"
echo "Verified failure-reason contract and legacy-route parity: 11 entries"
echo "Verified city contract, distinct ordering, and legacy-route parity"
