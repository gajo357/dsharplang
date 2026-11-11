import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

test('JavaScript documentation checks preserve local links, conflicts and symlink protection', () => {
  const root = mkdtempSync(join(tmpdir(),'dsharp-docs-'));
  const check = () => spawnSync(process.execPath,[fileURLToPath(new URL('../check-docs.mjs',import.meta.url)),root],{encoding:'utf8'});
  try {
    mkdirSync(join(root,'docs'));
    for (const name of ['a','b','c','d']) writeFileSync(join(root,`docs/${name}.md`),'Text');
    writeFileSync(join(root,'README.md'),'[Local](docs/a.md#heading)\n```text\n[Example](missing.md)\n```');
    assert.equal(check().status,0);
    for (const text of ['', '[Broken](missing.md)', '<<<<<<< conflicting change', '[Outside](../outside.md)']) {
      writeFileSync(join(root,'README.md'),text); assert.notEqual(check().status,0);
    }
    writeFileSync(join(root,'README.md'),'Text');
    symlinkSync(join(root,'README.md'),join(root,'docs/link.md')); assert.notEqual(check().status,0);
    rmSync(join(root,'docs/link.md'));
    symlinkSync(root,join(root,'docs/loop')); assert.notEqual(check().status,0);
  } finally { rmSync(root,{recursive:true,force:true}); }
});
