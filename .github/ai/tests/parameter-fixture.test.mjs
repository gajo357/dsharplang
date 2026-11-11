import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(new URL('../migrate-parameter-fixture.mjs', import.meta.url));
const oldFixture = '[InlineData("module Demo fn answer(x: int): int { 42 }", "DS1002")]';
const newFixture = '[InlineData("module Demo fn answer(x): int { 42 }", "DS1002")]';

test('CI fixture migration changes only the intended trusted assertion', () => {
    const root = mkdtempSync(join(tmpdir(), 'dsharp-fixture-migration-'));
    const trusted = join(root, 'trusted');
    const candidate = join(root, 'candidate');
    const relative = 'tests/DSharp.Compiler.Tests/CompilerTests.cs';
    const basePath = join(trusted, relative);
    const candidatePath = join(candidate, relative);
    const baseSha = '4cf7fd1f9e88c97eae64be6eee1c3e5186ab642a';
    const base = `prefix\n${oldFixture}\nsuffix\n`;
    const migrated = base.replace(oldFixture, newFixture);
    try {
        for (const p of [basePath, candidatePath]) mkdirSync(dirname(p), { recursive: true });
        const run = (sha = baseSha) => spawnSync(process.execPath, [script, trusted, candidate, sha], { encoding: 'utf8' });
        writeFileSync(basePath, base);
        writeFileSync(candidatePath, migrated);
        let result = run();
        assert.equal(result.status, 0, result.stderr);
        assert.equal(readFileSync(basePath, 'utf8'), migrated);
        result = run();
        assert.equal(result.status, 0, result.stderr);
        assert.equal(readFileSync(basePath, 'utf8'), migrated);
        writeFileSync(basePath, base);
        writeFileSync(candidatePath, migrated + '// unrelated change\n');
        result = run();
        assert.equal(result.status, 0, result.stderr);
        assert.equal(readFileSync(basePath, 'utf8'), base);
        writeFileSync(candidatePath, migrated);
        result = run('f'.repeat(40));
        assert.equal(result.status, 0, result.stderr);
        assert.equal(readFileSync(basePath, 'utf8'), base);
        writeFileSync(basePath, `${base}${oldFixture}\n`);
        result = run();
        assert.notEqual(result.status, 0);
    } finally { rmSync(root, { recursive: true, force: true }); }
});
