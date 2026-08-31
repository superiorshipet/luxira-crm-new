#!/usr/bin/env bash

set -euo pipefail

legacy_root="${1:-/media/superior/New Volume/luxira work/luxira-crm-main}"

if [[ ! -f "$legacy_root/Crm_LotusBlue.csproj" || ! -d "$legacy_root/Controllers" ]]; then
    echo "Legacy root is invalid: $legacy_root" >&2
    exit 2
fi

cd "$legacy_root"

count_matches() {
    local pattern="$1"
    shift
    rg --no-heading -g '*.cs' "$pattern" "$@" 2>/dev/null | wc -l | tr -d ' '
}

echo "# Generated Legacy Contract Inventory"
echo
echo "Generated from: \`$legacy_root\`"
echo
echo "This is a static candidate inventory. Conventional MVC routes, inherited attributes,"
echo "filters, runtime branches, and response shapes still require manual characterization."
echo

echo "## Framework and scale"
echo
echo "- Target framework: \`$(sed -n 's:.*<TargetFramework>\(.*\)</TargetFramework>.*:\1:p' Crm_LotusBlue.csproj | head -1)\`"
echo "- Controller files: $(find Controllers -maxdepth 1 -name '*.cs' | wc -l | tr -d ' ')"
echo "- HTTP verb attributes: $(count_matches '\[Http(Get|Post|Put|Delete|Patch)' Controllers)"
echo "- Explicit \`[ApiController]\` attributes: $(count_matches '\[ApiController\]' Controllers)"
echo "- Explicit \`[Authorize]\` attributes: $(count_matches '\[Authorize' Controllers)"
echo "- Explicit \`[AllowAnonymous]\` attributes: $(count_matches '\[AllowAnonymous' Controllers)"
echo "- \`[FromBody]\` parameters: $(count_matches '\[FromBody\]' Controllers)"
echo

echo "## Controller candidates"
echo
echo "| Controller file | Lines | HTTP verbs | Authorize | Anonymous | Routes |"
echo "|---|---:|---:|---:|---:|---:|"

while IFS= read -r -d '' file; do
    lines=$(wc -l < "$file" | tr -d ' ')
    verbs=$(rg -c '\[Http(Get|Post|Put|Delete|Patch)' "$file" 2>/dev/null || true)
    authorize=$(rg -c '\[Authorize' "$file" 2>/dev/null || true)
    anonymous=$(rg -c '\[AllowAnonymous' "$file" 2>/dev/null || true)
    routes=$(rg -c '\[(Route|Http(Get|Post|Put|Delete|Patch))' "$file" 2>/dev/null || true)
    echo "| \`$file\` | $lines | ${verbs:-0} | ${authorize:-0} | ${anonymous:-0} | ${routes:-0} |"
done < <(find Controllers -maxdepth 1 -name '*.cs' -print0 | sort -z)

echo
echo "## Route and verb declarations"
echo
echo '```text'
rg -n --no-heading -g '*.cs' '\[(Route|Http(Get|Post|Put|Delete|Patch))' Controllers || true
echo '```'
echo

echo "## Authorization declarations"
echo
echo '```text'
rg -n --no-heading -g '*.cs' '\[(Authorize|AllowAnonymous)' Controllers || true
echo '```'
echo

echo "## SignalR hubs and mapped routes"
echo
echo '```text'
rg -n --no-heading 'MapHub|class .*Hub' Program.cs Hubs -g '*.cs' || true
echo '```'
echo

echo "## Hosted-service registrations"
echo
echo '```text'
rg -n --no-heading 'AddHostedService' Program.cs || true
echo '```'
echo

echo "## Flutter compatibility surface"
echo
echo '```text'
rg -n --no-heading -g '*.cs' 'X-Flutter-App|FlutterApiFilter|JwtBearer' Program.cs Controllers Filters ApiToken || true
echo '```'
echo

echo "## Runtime schema mutation candidates"
echo
echo '```text'
rg -n --no-heading -g '*.cs' 'ALTER TABLE|CREATE TABLE|CREATE INDEX|COL_LENGTH\(|OBJECT_ID\(' Program.cs Controllers Services || true
echo '```'
echo

echo "## External-side-effect candidates"
echo
echo '```text'
rg -n --no-heading -g '*.cs' 'IAmazonS3|Amazon\.S3|CloudWatch|Cloudflare|Camex|Sandoog|Infobip|WhatsApp|SmtpClient|SendMailAsync|HttpClient|IHubContext|Clients\.' Controllers Services Hubs || true
echo '```'

