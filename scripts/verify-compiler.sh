#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
reports="${COMPILER_REPORT_DIR:-$PWD/artifacts/compiler}"
mkdir -p "$reports"
node -e 'if (Number(process.versions.node.split(".")[0]) < 24) throw Error("Node 24+ required")'
npm ci --ignore-scripts --no-audit --no-fund
rm -f "$reports/compiler.trx" "$reports/features.trx"
test "$(dotnet --version)" = 10.0.100
dotnet restore DSharp.slnx --locked-mode
dotnet build DSharp.slnx --no-restore --configuration Release
dotnet test tests/DSharp.Compiler.Tests --no-build --no-restore --configuration Release --logger 'trx;LogFileName=compiler.trx' --results-directory "$reports"
node scripts/check-test-results.mjs "$reports/compiler.trx"

if node --input-type=module -e 'import { files } from "./scripts/validation.mjs"; process.exit(files("tests/features").some(p => p.endsWith(".cs")) ? 0 : 1)'; then
  dotnet test tests/DSharp.Compiler.Tests --no-restore --configuration Release -p:DSharpTestGroup=Features --logger 'trx;LogFileName=features.trx' --results-directory "$reports"
  node scripts/check-test-results.mjs "$reports/features.trx"
fi
