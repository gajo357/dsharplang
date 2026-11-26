import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../..', import.meta.url));

test('application verifier rejects a constant that preserves the summary result', { timeout: 240000 }, () => {
  const snapshot = mkdtempSync(resolve(tmpdir(), 'dsharp-app-negative-'));
  try {
    const archive = spawnSync('git', ['archive', '--format=tar', 'HEAD'], { cwd: root, maxBuffer: 32 * 1024 * 1024 });
    assert.equal(archive.status, 0, 'Could not export committed source');
    const extract = spawnSync('tar', ['-xf', '-', '-C', snapshot], { input: archive.stdout, maxBuffer: 1024 * 1024 });
    assert.equal(extract.status, 0, 'Could not extract committed source');
    const source = resolve(snapshot, 'examples/inventory-app/inventory/stock.ds');
    const original = readFileSync(source, 'utf8');
    assert.match(original, /    received - sold\n/);
    writeFileSync(source, original.replace('    received - sold\n', '    42\n'));
    const result = spawnSync('bash', ['../../scripts/verify-application-example.sh'], {
      cwd: resolve(snapshot, 'examples/inventory-app'), encoding: 'utf8', timeout: 210000, maxBuffer: 1024 * 1024
    });
    assert.notEqual(result.status, 0, 'A constant implementation must fail');
    assert.match(result.stderr, /Inventory remaining calculation was incorrect\./);
    assert.doesNotMatch(result.stderr, /Inventory result was not 42\./);
  } finally {
    rmSync(snapshot, { recursive: true, force: true });
  }
});
