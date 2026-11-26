import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync, realpathSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { isAbsolute, relative, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../..', import.meta.url));
const command = 'bash ../../scripts/verify-application-example.sh';

test('application instructions work from a clean committed source snapshot', { timeout: 240000 }, () => {
  const snapshot = mkdtempSync(resolve(tmpdir(), 'dsharp-app-source-'));
  try {
    const archive = spawnSync('git', ['archive', '--format=tar', 'HEAD'], { cwd: root, maxBuffer: 32 * 1024 * 1024 });
    assert.equal(archive.status, 0, 'Could not export committed source');
    const extract = spawnSync('tar', ['-xf', '-', '-C', snapshot], { input: archive.stdout, maxBuffer: 1024 * 1024 });
    assert.equal(extract.status, 0, 'Could not extract committed source');
    assert.equal(existsSync(resolve(snapshot, '.git')), false);

    for (const directory of ['templates/application', 'examples/inventory-app']) {
      const folder = resolve(snapshot, directory);
      assert.equal(readFileSync(resolve(folder, 'CLAUDE.md'), 'utf8'), '@AGENTS.md\n');
      assert.ok(readFileSync(resolve(folder, 'AGENTS.md'), 'utf8').length > 0);
    }
    const example = resolve(snapshot, 'examples/inventory-app');
    for (const file of ['AGENTS.md', 'CHEATSHEET.md']) {
      const text = readFileSync(resolve(example, file), 'utf8');
      const links = [...text.matchAll(/\]\(([^)]+)\)/g)].map(match => match[1]);
      assert.ok(links.length > 0, `${file} must link to its sources`);
      for (const link of links) {
        const target = realpathSync(resolve(example, link));
        const fromRoot = relative(snapshot, target);
        assert.ok(fromRoot !== '..' && !fromRoot.startsWith('../') && !isAbsolute(fromRoot), `Link escapes snapshot: ${link}`);
      }
      const commands = [...text.matchAll(/```sh\n([\s\S]*?)\n```/g)].map(match => match[1]);
      assert.deepEqual(commands, [command], `${file} must document the verified command`);
    }
    const run = spawnSync('bash', ['../../scripts/verify-application-example.sh'], {
      cwd: example, encoding: 'utf8', timeout: 210000, maxBuffer: 1024 * 1024
    });
    assert.equal(run.status, 0, `${run.stdout}\n${run.stderr}`);
    assert.match(run.stdout, /Inventory example checked, compiled and returned 42\./);
  } finally {
    rmSync(snapshot, { recursive: true, force: true });
  }
});
