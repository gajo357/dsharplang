import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const example = resolve(fileURLToPath(new URL('../../../examples/inventory-app', import.meta.url)));

// The existing inventory-cheatsheet test verifies marked blocks against source;
// reject D# fences that are not covered by that source comparison.
function requireSourceForEveryDSharpBlock(guide) {
  const all = [...guide.matchAll(/^ {0,3}(?:`{3,}|~{3,})[ \t]*(?:dsharp|d#|ds)(?=[ \t]|$).*$/gim)];
  const marked = [...guide.matchAll(/^<!-- source: [^\n]+ -->\n```dsharp\n[\s\S]*?^```$/gm)];
  assert.ok(all.length > 0, 'The reference must include verified D# examples.');
  assert.equal(all.length, marked.length, 'Each D# block must be source-marked.');
}

test('inventory reference has no unverified D# blocks', () => {
  const guide = readFileSync(resolve(example, 'CHEATSHEET.md'), 'utf8');
  requireSourceForEveryDSharpBlock(guide);
  for (const fence of ['```dsharp', '```DS', '~~~d#']) {
    assert.throws(() => requireSourceForEveryDSharpBlock(`${guide}\n${fence}\nexample\n${fence[0].repeat(3)}\n`));
  }
});
