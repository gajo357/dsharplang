#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
reports="${COMPILER_REPORT_DIR:-$PWD/artifacts/compiler}"
mkdir -p "$reports"
rm -f "$reports/compiler.trx"
test "$(dotnet --version)" = 10.0.100
dotnet restore DSharp.slnx --locked-mode
dotnet build DSharp.slnx --no-restore --configuration Release
dotnet test tests/DSharp.Compiler.Tests --no-build --no-restore --configuration Release --logger 'trx;LogFileName=compiler.trx' --results-directory "$reports"
python3 scripts/check-test-results.py "$reports/compiler.trx"
