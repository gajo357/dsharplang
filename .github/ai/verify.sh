#!/usr/bin/env bash
set -euo pipefail
trusted="$(cd "$(dirname "$0")" && pwd)"
target="${1:-$(cd "$trusted/../.." && pwd)}"
reports="${AI_REPORT_DIR:-$(mktemp -d /tmp/dsharp-verification.XXXXXX)}"
mkdir -p "$reports"
python3 "$trusted/check_docs.py" "$target" | tee "$reports/documentation.json"
# Automation tests always come from the trusted checkout, never the candidate.
node --test --test-reporter=spec --test-reporter-destination=stdout \
  --test-reporter=junit --test-reporter-destination="$reports/automation.xml" "$trusted/tests/"*.test.mjs
python3 - "$reports/automation.xml" <<'PY'
import sys
from xml.etree import ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
cases = root.findall('.//testcase')
assert len(cases) >= 7, 'Expected automation tests were not discovered'
assert not root.findall('.//skipped'), 'Required tests were skipped'
print(f'Verified {len(cases)} executed automation tests; reports saved.')
PY

python3 "$trusted/verify-compiler.py" "$target" "$reports"
