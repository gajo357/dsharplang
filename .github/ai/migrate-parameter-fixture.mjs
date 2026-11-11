// One-time CI migration for #20. Preserve the base test suite exactly except for
// the obsolete assertion that an explicitly typed parameter is invalid.
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [trusted, candidate, baseSha] = process.argv.slice(2);
if (!trusted || !candidate || !baseSha) throw new Error('Expected trusted/candidate checkout paths and base SHA');
const migrationBase = '4cf7fd1f9e88c97eae64be6eee1c3e5186ab642a';
const relative = 'tests/DSharp.Compiler.Tests/CompilerTests.cs';
const oldFixture = '[InlineData("module Demo fn answer(x: int): int { 42 }", "DS1002")]';
const newFixture = '[InlineData("module Demo fn answer(x): int { 42 }", "DS1002")]';
const trustedPath = join(trusted, relative);
const base = readFileSync(trustedPath, 'utf8');
if (base.includes(oldFixture) && baseSha === migrationBase) {
    if (base.split(oldFixture).length !== 2) throw new Error('Ambiguous trusted parameter fixture');
    const migrated = base.replace(oldFixture, newFixture);
    const proposed = readFileSync(join(candidate, relative), 'utf8');
    if (proposed === migrated) {
        writeFileSync(trustedPath, migrated);
        console.log('Applied the exact reviewed parameter-fixture migration to the temporary trusted checkout.');
    } else {
        console.log('Candidate does not contain exactly the fixture migration; trusted checkout remains unchanged.');
    }
} else {
    console.log('Fixture migration does not apply to this base; trusted checkout remains unchanged.');
}
