import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const pilotUrl = new URL('../pilot.mjs', import.meta.url);
const validationUrl = new URL('../../../scripts/validation.mjs', import.meta.url);

test('compiler pilots isolate existing root and nested feature tests without changing them', () => {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-pilot-regression-'));
  function put(path, content) {
    const target = join(root, path);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, content);
  }
  try {
    put('src/DSharp.Compiler/Compiler.cs', '// Original compiler fixture.\n');
    const existing = ['tests/features/Existing.cs', 'tests/features/nested/Other.cs'];
    for (const path of existing) put(path, '[Fact] public void Existing() {}\n');
    // Run the real driver, but import the real utility from its installed checkout.
    put('.github/ai/pilot.mjs', readFileSync(pilotUrl, 'utf8').replace(
      "'../../scripts/validation.mjs'", JSON.stringify(validationUrl.href)));
    // Model the verifier's staging contract: trusted features first, candidate
    // files overlaid by relative path. No Docker or C# execution is claimed here.
    put('.github/ai/verify-compiler.mjs', `
import { readFileSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { files } from ${JSON.stringify(validationUrl.href)};
const trusted = fileURLToPath(new URL('../..', import.meta.url));
const [candidate, reports] = process.argv.slice(2);
const staged = new Map();
for (const tree of [trusted, candidate])
  for (const path of files(join(tree, 'tests/features')).filter(p => p.endsWith('.cs')))
    staged.set(relative(tree, path), readFileSync(path, 'utf8'));
const total = [...staged.values()].filter(text => text.includes('[Fact]')).length;
const broken = readFileSync(join(candidate, 'src/DSharp.Compiler/Compiler.cs'), 'utf8').includes('this will not compile');
writeFileSync(join(reports, 'compiler.json'), JSON.stringify({
  acceptance: { total: 18 }, features: { total }
}));
process.exit(broken || total === 0 ? 1 : 0);
`);
    const result = spawnSync(process.execPath, [join(root, '.github/ai/pilot.mjs'), join(root, 'reports')], { encoding: 'utf8', timeout: 30000 });
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const summary = JSON.parse(readFileSync(join(root, 'reports/pilot.json'), 'utf8'));
    assert.equal(summary.success.exitCode, 0);
    assert.equal(summary.success.compiler.features.total, 1);
    assert.equal(summary['build-failure'].exitCode, 1);
    assert.equal(summary['zero-tests'].exitCode, 1);
    assert.equal(summary['zero-tests'].compiler.features.total, 0);
    for (const path of existing) assert.equal(readFileSync(join(root, path), 'utf8'), '[Fact] public void Existing() {}\n');
    assert.equal(readFileSync(join(root, 'src/DSharp.Compiler/Compiler.cs'), 'utf8'), '// Original compiler fixture.\n');
  } finally { rmSync(root, { recursive: true, force: true }); }
});
