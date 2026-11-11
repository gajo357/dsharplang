#!/usr/bin/env bash
set -euo pipefail
trusted="$(cd "$(dirname "$0")" && pwd)"
target="${1:-$(cd "$trusted/../.." && pwd)}"
reports="${AI_REPORT_DIR:-$(mktemp -d /tmp/dsharp-verification.XXXXXX)}"
mkdir -p "$reports"
(cd "$trusted/../.." && npm ci --ignore-scripts --no-audit --no-fund)
node "$trusted/check-docs.mjs" "$target" | tee "$reports/documentation.json"
# Automation tests always come from the trusted checkout, never the candidate.
node --test --test-reporter=spec --test-reporter-destination=stdout \
  --test-reporter=junit --test-reporter-destination="$reports/automation.xml" "$trusted/tests/"*.test.mjs
node "$trusted/../../scripts/check-automation-results.mjs" "$reports/automation.xml"
node "$trusted/verify-compiler.mjs" "$target" "$reports"
