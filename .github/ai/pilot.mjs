// Disposable offline compiler pilots. No GitHub/model calls or publishing authority.
import assert from 'node:assert/strict';
import { copyFileSync, mkdirSync, mkdtempSync, openSync, closeSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { files } from '../../scripts/validation.mjs';

const trusted = fileURLToPath(new URL('../..', import.meta.url));
const reports = resolve(process.argv[2] || join(trusted, 'artifacts/pilot'));
mkdirSync(reports, { recursive: true });
const summary = {};
const candidate = mkdtempSync(join(tmpdir(), 'dsharp-pilot-'));
try {
  for (const source of files(join(trusted, 'src'))) {
    const destination = join(candidate, relative(trusted, source));
    mkdirSync(dirname(destination), { recursive: true }); copyFileSync(source, destination);
  }
  const source = join(candidate, 'src/DSharp.Compiler/Compiler.cs');
  const original = readFileSync(source, 'utf8');
  writeFileSync(source, original + '\n// Offline code-task pilot.\n');
  const feature = join(candidate, 'tests/features/PilotTests.cs');
  mkdirSync(dirname(feature), { recursive: true });
  writeFileSync(feature, String.raw`using Xunit;
using DSharp.Compiler;
public sealed class PilotTests {
    [Fact] public void CommentedLiteralBindsTo42() {
        var syntax = new Parser(Lexer.Lex("module Demo // pilot\nfn answer(): int { 42 }")).Parse();
        Assert.Equal(42, Assert.Single(Binder.Bind(syntax).Functions).Value);
    }
}
`);
  for (const scenario of ['success', 'build-failure', 'zero-tests']) {
    if (scenario === 'build-failure') writeFileSync(source, original + '\nthis will not compile\n');
    if (scenario === 'zero-tests') {
      writeFileSync(source, original);
      writeFileSync(feature, '// No discoverable tests: verification must fail.\n');
    }
    const destination = join(reports, scenario); mkdirSync(destination, { recursive: true });
    const logPath = join(destination, 'verification.log');
    const log = openSync(logPath, 'w');
    let result;
    try { result = spawnSync(process.execPath, [join(trusted, '.github/ai/verify-compiler.mjs'), candidate, destination], { stdio: ['ignore', log, log], timeout: 540000, killSignal: 'SIGKILL' }); }
    finally { closeSync(log); }
    if (result.error) throw result.error;
    assert(Number.isInteger(result.status), 'Pilot did not finish');
    const evidence = JSON.parse(readFileSync(join(destination, 'compiler.json'), 'utf8'));
    if (scenario === 'success') {
      assert(result.status === 0, readFileSync(logPath, 'utf8'));
      assert(evidence.acceptance.total >= 18 && evidence.features.total > 0);
    } else assert(result.status !== 0, `${scenario} incorrectly passed`);
    summary[scenario] = { exitCode: result.status, compiler: evidence };
  }
} finally { rmSync(candidate, { recursive: true, force: true }); }
writeFileSync(join(reports, 'pilot.json'), JSON.stringify(summary, null, 2));
console.log(JSON.stringify(summary, null, 2));
