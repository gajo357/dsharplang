#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
reports="${SQLITE_REPORT_DIR:-$PWD/artifacts/sqlite}"
mkdir -p "$reports"
node -e 'if (Number(process.versions.node.split(".")[0]) < 24) throw Error("Node 24+ required")'
npm ci --ignore-scripts --no-audit --no-fund
rm -f "$reports/sqlite-baseline.trx" "$reports/sqlite-features.trx"
test "$(dotnet --version)" = 11.0.100
project="tests/DSharp.Runtime.Sqlite.Tests/DSharp.Runtime.Sqlite.Tests.csproj"
dotnet restore "$project" --locked-mode
dotnet build "$project" --no-restore --configuration Release
dotnet test "$project" --no-build --no-restore --configuration Release --logger 'trx;LogFileName=sqlite-baseline.trx' --results-directory "$reports"
test -f "$reports/sqlite-baseline.trx" && test ! -L "$reports/sqlite-baseline.trx" && test "$(stat -c %s "$reports/sqlite-baseline.trx")" -le 512000
node --input-type=module -e 'import { compilerResults } from "./scripts/validation.mjs"; console.log(JSON.stringify({ ...compilerResults(process.argv[1]), suite: "sqlite-baseline" }))' "$reports/sqlite-baseline.trx"
dotnet test "$project" --no-restore --configuration Release -p:DSharpTestGroup=SqliteFeatures --logger 'trx;LogFileName=sqlite-features.trx' --results-directory "$reports"
test -f "$reports/sqlite-features.trx" && test ! -L "$reports/sqlite-features.trx" && test "$(stat -c %s "$reports/sqlite-features.trx")" -le 512000
node --input-type=module -e 'import { compilerResults } from "./scripts/validation.mjs"; console.log(JSON.stringify({ ...compilerResults(process.argv[1]), suite: "sqlite-features" }))' "$reports/sqlite-features.trx"
