// Prove the proposed isolated SQLite verifier rejects an actual failing test.
// Both attempts operate on disposable candidate copies and fresh report paths.
import assert from 'node:assert/strict';
import { closeSync, copyFileSync, lstatSync, mkdirSync, mkdtempSync, openSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, parse, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { compilerResults, descendants, files, readXml } from '../../scripts/validation.mjs';

const trusted = fileURLToPath(new URL('../..', import.meta.url));
const reports = resolve(process.argv[2] || join(trusted, 'artifacts/sqlite-pilot'));
const verifier = join(trusted, '.github/ai/verify-sqlite.mjs');
const testClass = 'SqliteVerifierFailurePilot';
const testName = `${testClass}.DeliberateFailure`;
const ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010';

// Reject links in every existing component before creating report directories
// or deleting a prior summary. Never follow a dangling link either.
function createSafeReportDirectory(path) {
  const root = parse(path).root;
  let current = root;
  for (const component of path.slice(root.length).split(sep).filter(Boolean)) {
    current = join(current, component);
    let stat;
    try { stat = lstatSync(current); }
    catch (error) {
      if (error.code !== 'ENOENT') throw error;
      mkdirSync(current);
      stat = lstatSync(current);
    }
    assert(stat.isDirectory() && !stat.isSymbolicLink(), `Unsafe report path: ${current}`);
  }
}

createSafeReportDirectory(reports);
// Never let an old successful pilot summary survive a failed new attempt.
rmSync(join(reports, 'pilot.json'), { force: true });
const candidate = mkdtempSync(join(tmpdir(), 'dsharp-sqlite-pilot-'));

async function run(destination) {
  const log = openSync(join(destination, 'verification.log'), 'w');
  try {
    return await new Promise((resolveRun, rejectRun) => {
      const child = spawn(process.execPath, [verifier, candidate, destination], {
        stdio: ['ignore', log, log], timeout: 540000, killSignal: 'SIGKILL'
      });
      child.once('error', rejectRun);
      child.once('close', (status, signal) => {
        if (!Number.isInteger(status)) rejectRun(new Error(`SQLite verifier did not finish normally (${signal})`));
        else resolveRun(status);
      });
    });
  } finally { closeSync(log); }
}

try {
  for (const folder of ['src/DSharp.Runtime', 'src/DSharp.Runtime.Sqlite', 'tests/sqlite-features']) {
    for (const source of files(join(trusted, folder))) {
      const destination = join(candidate, relative(trusted, source));
      mkdirSync(dirname(destination), { recursive: true });
      copyFileSync(source, destination);
    }
  }
  const featureDirectory = join(candidate, 'tests/sqlite-features');
  for (const path of files(featureDirectory).filter(path => path.endsWith('.cs')))
    assert(!readFileSync(path, 'utf8').includes(testClass), 'Pilot test class collides with an existing feature');
  const failure = join(featureDirectory, `${testClass}.cs`);
  assert(!files(featureDirectory).includes(failure), 'Pilot fixture collides with an existing test');
  mkdirSync(featureDirectory, { recursive: true });
  writeFileSync(failure, 'using Xunit;\npublic sealed class SqliteVerifierFailurePilot { [Fact] public void DeliberateFailure() { Assert.Fail("intentional SQLite verifier pilot"); } }\n');

  const trial = mkdtempSync(join(reports, 'failure-'));
  const status = await run(trial);
  assert.notEqual(status, 0, 'Intentional SQLite test failure did not fail verification');
  const evidence = JSON.parse(readFileSync(join(trial, 'sqlite.json'), 'utf8'));
  assert(evidence.build === 'success' && evidence.baseline?.total > 0, 'Failure occurred before the SQLite baseline passed');
  const baseline = compilerResults(join(trial, 'sqlite-baseline.trx'));
  assert.equal(baseline.total, evidence.baseline.total, 'SQLite baseline evidence disagrees with its TRX');
  const report = readXml(join(trial, 'sqlite-features.trx'));
  assert(report.name === 'TestRun' && report.namespace === ns, 'Expected a VSTest SQLite feature report');
  const results = descendants(report, 'UnitTestResult', ns);
  const ids = results.map(item => item.attributes.testId);
  assert(results.length > 0 && ids.every(Boolean) && new Set(ids).size === ids.length, 'SQLite feature results are missing or duplicated');
  assert(results.some(item => item.attributes.outcome === 'Failed' && item.attributes.testName?.includes(testName)),
    'Expected named intentional failure was not found in SQLite feature results');

  rmSync(failure);
  const clean = mkdtempSync(join(reports, 'clean-'));
  const cleanStatus = await run(clean);
  assert.equal(cleanStatus, 0, 'Clean candidate failed isolated SQLite verification after removing the probe');
  const cleanEvidence = JSON.parse(readFileSync(join(clean, 'sqlite.json'), 'utf8'));
  assert(cleanEvidence.build === 'success' && cleanEvidence.baseline?.total > 0 && cleanEvidence.features?.total > 0,
    'Clean SQLite verifier evidence is incomplete');
  const cleanBaseline = compilerResults(join(clean, 'sqlite-baseline.trx'));
  const cleanFeatures = compilerResults(join(clean, 'sqlite-features.trx'));
  assert.equal(cleanBaseline.total, cleanEvidence.baseline.total, 'Clean SQLite baseline evidence disagrees with its TRX');
  assert.equal(cleanFeatures.total, cleanEvidence.features.total, 'Clean SQLite feature evidence disagrees with its TRX');
  writeFileSync(join(reports, 'pilot.json'), JSON.stringify({
    exitCode: status, baseline: evidence.baseline, intentionalTest: testName, outcome: 'Failed',
    cleanExitCode: cleanStatus, clean: cleanEvidence
  }, null, 2));
  console.log('The isolated SQLite verifier rejected the intentional test failure and passed after its removal.');
} finally {
  rmSync(candidate, { recursive: true, force: true });
}
