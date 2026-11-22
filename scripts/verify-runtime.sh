#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
reports="${RUNTIME_REPORT_DIR:-$PWD/artifacts/runtime}"
mkdir -p "$reports"
node -e 'if (Number(process.versions.node.split(".")[0]) < 24) throw Error("Node 24+ required")'
npm ci --ignore-scripts --no-audit --no-fund
rm -f "$reports/runtime-baseline.trx" "$reports/runtime-features.trx"
test "$(dotnet --version)" = 11.0.100
dotnet restore DSharp.slnx --locked-mode
dotnet build DSharp.slnx --no-restore --configuration Release
dotnet test tests/DSharp.Runtime.Tests --no-build --no-restore --configuration Release --logger 'trx;LogFileName=runtime-baseline.trx' --results-directory "$reports"
node --input-type=module -e 'import { compilerResults } from "./scripts/validation.mjs"; console.log(JSON.stringify({ ...compilerResults(process.argv[1]), suite: "runtime-baseline" }))' "$reports/runtime-baseline.trx"
dotnet test tests/DSharp.Runtime.Tests --no-restore --configuration Release -p:DSharpTestGroup=RuntimeFeatures --logger 'trx;LogFileName=runtime-features.trx' --results-directory "$reports"
node --input-type=module -e 'import { compilerResults } from "./scripts/validation.mjs"; console.log(JSON.stringify({ ...compilerResults(process.argv[1]), suite: "runtime-features" }))' "$reports/runtime-features.trx"
