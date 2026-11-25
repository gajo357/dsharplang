import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const example = resolve(fileURLToPath(new URL('../../../examples/inventory-app', import.meta.url)));
test('inventory reference stays aligned with verified source and local links', () => {
  const guide = readFileSync(resolve(example, 'CHEATSHEET.md'), 'utf8');
  const excerpts = [...guide.matchAll(/<!-- source: ([^\n]+) -->\n```dsharp\n([\s\S]*?)```/g)];
  assert.deepEqual(excerpts.map(match => match[1]), ['inventory/stock.ds', 'app/summary.ds']);
  for (const [, path, code] of excerpts) assert.equal(code, readFileSync(resolve(example, path), 'utf8'));
  const links = [...guide.matchAll(/\]\(([^)]+)\)/g)].map(match => match[1]);
  assert.ok(links.length > 0);
  for (const link of links) assert.ok(existsSync(resolve(example, link)), `Missing reference link: ${link}`);
});
