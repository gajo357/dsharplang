#!/usr/bin/env bash
set -euo pipefail
# Pinned official Linux amd64 release; expected hash verified against release checksums.txt.
test "$(uname -s)-$(uname -m)" = Linux-x86_64
destination=/workspace/.tools
mkdir -p "$destination"
expected=c2378d85466d29cdbdfebd0e513557b5cbec18d34f44e7cc00c2fa323687a153
if ! printf '%s  %s\n' "$expected" "$destination/gh-aw" | sha256sum --check --status; then
  staging="$(mktemp -d /tmp/dsharp-tools.XXXXXX)"
  trap 'rm -rf "$staging"' EXIT
  curl --fail --show-error --location --max-time 120 \
    https://github.com/github/gh-aw/releases/download/v1.2.2/linux-amd64 -o "$staging/gh-aw"
  printf '%s  %s\n' "$expected" "$staging/gh-aw" | sha256sum --check
  install -m 755 "$staging/gh-aw" "$destination/gh-aw"
fi
"$destination/gh-aw" version
