import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../..', import.meta.url));

function directoryModes(directory, mode) {
  for (const entry of readdirSync(directory, { withFileTypes: true }))
    if (entry.isDirectory()) directoryModes(resolve(directory, entry.name), mode);
  chmodSync(directory, mode);
}

test('application command builds without writing to compiler source directories', { timeout: 240000 }, () => {
  const snapshot = mkdtempSync(resolve(tmpdir(), 'dsharp-app-readonly-'));
  const projects = ['src/DSharp.Cli', 'src/DSharp.Compiler'].map(path => resolve(snapshot, path));
  let protectedSources = false;
  try {
    const archive = spawnSync('git', ['archive', '--format=tar', 'HEAD'], { cwd: root, maxBuffer: 32 * 1024 * 1024 });
    assert.equal(archive.status, 0, 'Could not export committed source');
    const extract = spawnSync('tar', ['-xf', '-', '-C', snapshot], { input: archive.stdout, maxBuffer: 1024 * 1024 });
    assert.equal(extract.status, 0, 'Could not extract committed source');
    protectedSources = true;
    for (const project of projects) directoryModes(project, 0o555);
    const result = spawnSync('bash', ['../../scripts/verify-application-example.sh'], {
      cwd: resolve(snapshot, 'examples/inventory-app'), encoding: 'utf8', timeout: 210000, maxBuffer: 1024 * 1024
    });
    assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}\n${result.error ?? ''}`);
    assert.match(result.stdout, /Inventory example checked, compiled and returned 42\./);
    for (const project of projects)
      for (const output of ['bin', 'obj']) assert.equal(existsSync(resolve(project, output)), false);
  } finally {
    if (protectedSources) for (const project of projects) directoryModes(project, 0o755);
    rmSync(snapshot, { recursive: true, force: true });
  }
});
