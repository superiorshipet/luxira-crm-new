#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verification_port="${LUXIRA_OPENAPI_TEST_PORT:-5187}"
base_url="http://127.0.0.1:${verification_port}"
temporary_directory="$(mktemp -d)"
server_log="$temporary_directory/server.log"
openapi_document="$temporary_directory/openapi-v1.json"
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
    --project src/Luxira.Api/Luxira.Api.csproj \
    --no-build \
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

node tools/check-postman-coverage.mjs \
    "$openapi_document" \
    postman/coverage-manifest.json \
    postman/Luxira.Api.postman_collection.json

operation_count="$(
    jq '[.paths[] | to_entries[] | select(.value.operationId != null)] | length' \
        "$openapi_document"
)"

echo "Verified Postman import document at $base_url/swagger/v1/swagger.json"
echo "Published operations: $operation_count"

