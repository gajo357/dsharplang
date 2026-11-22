// Exercise the real offline runtime verifier with one intentionally failing test.
import assert from 'node:assert/strict';
import { closeSync, copyFileSync, mkdirSync, mkdtempSync, openSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { descendants, files, readXml } from '../../scripts/validation.mjs';

const trusted = fileURLToPath(new URL('../..', import.meta.url));
const reports = resolve(process.argv[2] || join(trusted, 'artifacts/runtime-pilot'));
mkdirSync(reports, { recursive: true });
// Every invocation must inspect evidence produced by this attempt, never a
// stale failed TRX from an earlier run after a later infrastructure failure.
rmSync(join(reports, 'pilot.json'), { force: true });
const trial = mkdtempSync(join(reports, 'trial-'));
const candidate = mkdtempSync(join(tmpdir(), 'dsharp-runtime-pilot-'));
try {
  for (const folder of ['src/DSharp.Runtime', 'tests/runtime-features']) {
    for (const source of files(join(trusted, folder))) {
      const destination = join(candidate, relative(trusted, source));
      mkdirSync(dirname(destination), { recursive: true });
      copyFileSync(source, destination);
    }
  }
  const failure = join(candidate, 'tests/runtime-features/RuntimeVerifierFailurePilot.cs');
  assert(!files(join(candidate, 'tests/runtime-features')).includes(failure), 'Pilot fixture collides with an existing test');
  mkdirSync(dirname(failure), { recursive: true });
  writeFileSync(failure, 'using Xunit;\npublic sealed class RuntimeVerifierFailurePilot { [Fact] public void DeliberateFailure() { Assert.Fail("intentional runtime verifier pilot"); } }\n');
  const logPath = join(trial, 'verification.log');
  const log = openSync(logPath, 'w');
  let result;
  try { result = spawnSync(process.execPath, [join(trusted, '.github/ai/verify-runtime.mjs'), candidate, trial], { stdio: ['ignore', log, log], timeout: 540000, killSignal: 'SIGKILL' }); }
  finally { closeSync(log); }
  if (result.error) throw result.error;
  assert(Number.isInteger(result.status) && result.status !== 0, 'Intentional failing runtime test did not fail verification');
  const evidence = JSON.parse(readFileSync(join(trial, 'runtime.json'), 'utf8'));
  assert(evidence.build === 'success' && evidence.baseline?.total > 0, 'Pilot failed before the trusted runtime baseline passed');
  const report = readXml(join(trial, 'runtime-features.trx'));
  const results = descendants(report, 'UnitTestResult', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010');
  assert(results.some(r => r.attributes.outcome === 'Failed' && r.attributes.testName?.includes('RuntimeVerifierFailurePilot.DeliberateFailure')), 'Expected intentional test failure not found in runtime TRX');
  writeFileSync(join(reports, 'pilot.json'), JSON.stringify({ exitCode: result.status, baseline: evidence.baseline, intentionalTest: 'RuntimeVerifierFailurePilot.DeliberateFailure', outcome: 'Failed' }, null, 2));
  console.log('The real isolated verifier rejected RuntimeVerifierFailurePilot.DeliberateFailure.');
} finally {
  rmSync(candidate, { recursive: true, force: true });
}
