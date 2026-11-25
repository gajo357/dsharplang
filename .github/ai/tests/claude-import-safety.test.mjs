import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync, chmodSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const verifier = join(root, 'scripts/verify-claude-import.mjs');
const fakeVersion = "if (process.argv[2] === '--version') { console.log('2.1.332 (Claude Code)'); process.exit(0); }";

function runFake(source) {
  const directory = mkdtempSync(join(tmpdir(), 'dsharp-import-test-'));
  try {
    const fake = join(directory, 'claude');
    writeFileSync(fake, `#!/usr/bin/env node\n${source}\n`);
    chmodSync(fake, 0o700);
    return spawnSync(process.execPath, [verifier, fake], {
      cwd: root, encoding: 'utf8', timeout: 60000,
      env: { ...process.env, DSHARP_TEST_SECRET: 'synthetic-sensitive-value' }
    });
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

test('version probe and local import checks exclude caller secrets', { timeout: 65000 }, () => {
  const result = runFake(`
    if (process.env.DSHARP_TEST_SECRET) { console.error(process.env.DSHARP_TEST_SECRET); process.exit(81); }
    ${fakeVersion}
    const fs = require('node:fs');
    const body = fs.readFileSync('CLAUDE.md', 'utf8') === '@AGENTS.md\\n'
      ? fs.readFileSync('AGENTS.md', 'utf8') : '';
    fetch(process.env.ANTHROPIC_BASE_URL + '/v1/messages', { method: 'POST', body })
      .then(response => { if (!response.ok) process.exit(1); console.log('OK'); })
      .catch(() => process.exit(1));
  `);
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /relative imports passed/);
  assert.doesNotMatch(result.stdout + result.stderr, /synthetic-sensitive-value/);
});

test('failed subprocess diagnostics are redacted', { timeout: 65000 }, () => {
  const result = runFake(`
    ${fakeVersion}
    console.error('synthetic-sensitive-diagnostic');
    process.exit(7);
  `);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Claude did not complete the local mock run/);
  assert.doesNotMatch(result.stdout + result.stderr, /synthetic-sensitive-diagnostic/);
});

test('version probe failures do not expose subprocess output', { timeout: 65000 }, () => {
  for (const exitCode of [0, 7]) {
    const result = runFake(`
      console.log('synthetic-sensitive-version');
      console.error('synthetic-sensitive-diagnostic');
      process.exit(${exitCode});
    `);
    assert.notEqual(result.status, 0);
    assert.doesNotMatch(result.stdout + result.stderr, /synthetic-sensitive-(version|diagnostic)/);
  }
});
