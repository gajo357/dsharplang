#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
node -e 'if (Number(process.versions.node.split(".")[0]) < 24) throw Error("Node 24+ required")'
npm ci --ignore-scripts --no-audit --no-fund
git --version
command -v unzip >/dev/null
test "$(dotnet --version)" = 10.0.100
command -v docker >/dev/null
echo 'Pinned compiler SDK and verification prerequisites available.'
