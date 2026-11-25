import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../..', import.meta.url));
test('application guidance imports and executable example', { timeout: 180000 }, () => {
  for (const directory of ['templates/application', 'examples/inventory-app']) {
    assert.equal(readFileSync(`${root}/${directory}/CLAUDE.md`, 'utf8'), '@AGENTS.md\n');
    assert.ok(readFileSync(`${root}/${directory}/AGENTS.md`, 'utf8').length > 0);
  }
  const example = resolve(root, 'examples/inventory-app');
  const guidance = readFileSync(resolve(example, 'AGENTS.md'), 'utf8');
  const links = [...guidance.matchAll(/\]\(([^)]+)\)/g)].map(match => match[1]);
  assert.ok(links.length > 0);
  for (const link of links) assert.ok(existsSync(resolve(example, link)), `Missing example link: ${link}`);
  const result = spawnSync('bash', ['../../scripts/verify-application-example.sh'], { cwd: example, encoding: 'utf8', timeout: 170000 });
  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
  assert.match(result.stdout, /Inventory example checked, compiled and returned 42\./);
});
