#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 -c 'import sys; assert sys.version_info >= (3, 11), "Python 3.11+ required"'
node -e 'if (Number(process.versions.node.split(".")[0]) < 24) throw Error("Node 24+ required")'
git --version
command -v unzip >/dev/null
echo 'Development prerequisites available; no application dependencies exist yet.'
