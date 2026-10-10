#!/usr/bin/env bash
# Fails when a backend NuGet package has a known vulnerability that has not been suppressed on the
# record. `dotnet list package --vulnerable` ignores <NuGetAuditSuppress>, so the suppressions in
# src/backend/Directory.Packages.props, each with the reason it is unreachable, are applied here;
# any advisory not listed there still fails the build.
set -euo pipefail

cd "$(dirname "$0")/../src/backend"

dotnet restore --locked-mode >/dev/null

found=$(dotnet list package --vulnerable --include-transitive --format json |
  jq -r '[.projects[]?.frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?)]
    | map(select(.vulnerabilities)) | .[] | .id as $id
    | .vulnerabilities[] | "\(.advisoryurl) \($id) \(.severity)"' | sort -u)

suppressed=$(grep -o '<NuGetAuditSuppress Include="[^"]*"' Directory.Packages.props |
  sed 's/.*Include="//; s/"$//' | sort -u)

open=$(comm -23 <(echo "$found" | cut -d' ' -f1 | sed '/^$/d' | sort -u) <(echo "$suppressed" | sed '/^$/d'))

if [ -n "$found" ]; then
  echo "Known advisories:"
  echo "$found"
fi

if [ -n "$open" ]; then
  echo "::error::A dependency has a known vulnerability that is not suppressed with a reason:"
  echo "$open"
  exit 1
fi

echo "No unsuppressed vulnerabilities."
