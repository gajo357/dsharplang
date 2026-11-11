#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
reports="${COMPILER_REPORT_DIR:-$PWD/artifacts/compiler}"
mkdir -p "$reports"
rm -f "$reports/compiler.trx" "$reports/features.trx"
test "$(dotnet --version)" = 10.0.100
dotnet restore DSharp.slnx --locked-mode
dotnet build DSharp.slnx --no-restore --configuration Release
dotnet test tests/DSharp.Compiler.Tests --no-build --no-restore --configuration Release --logger 'trx;LogFileName=compiler.trx' --results-directory "$reports"
python3 scripts/check-test-results.py "$reports/compiler.trx"

if python3 -c 'from pathlib import Path; import sys; sys.exit(not any(Path("tests/features").rglob("*.cs")))'; then
  dotnet test tests/DSharp.Compiler.Tests --no-restore --configuration Release -p:DSharpTestGroup=Features --logger 'trx;LogFileName=features.trx' --results-directory "$reports"
  python3 scripts/check-test-results.py "$reports/features.trx"
fi
