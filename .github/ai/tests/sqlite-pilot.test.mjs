import assert from 'node:assert/strict';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawn } from 'node:child_process';
import test from 'node:test';

const root = fileURLToPath(new URL('../../..', import.meta.url));
const pilot = join(root, '.github/ai/sqlite-pilot.mjs');
const validation = pathToFileURL(join(root, 'scripts/validation.mjs')).href;

// Fake verifier for exercising pilot orchestration only. The actual pilot still
// invokes the isolated verifier and must prove its own real failure and rerun.
const verifier = String.raw`
import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
const candidate = process.argv[2], reports = process.argv[3];
const probe = existsSync(join(candidate, 'tests/sqlite-features/SqliteVerifierFailurePilot.cs'));
if (!existsSync(join(candidate, 'src/DSharp.Runtime/Existing.cs'))) throw new Error('Candidate core source missing');
appendFileSync(process.env.PILOT_OBSERVATION_FILE, JSON.stringify({ candidate, probe }) + '\n');
mkdirSync(reports, { recursive: true });
if (process.env.PILOT_TEST_MODE === 'infrastructure') process.exit(1);
const trx = (name, passed) => '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>' +
  '<UnitTestResult testId="1" testName="' + name + '" outcome="' + (passed ? 'Passed' : 'Failed') + '" />' +
  '</Results><ResultSummary outcome="' + (passed ? 'Completed' : 'Failed') + '"><Counters total="1" executed="1" passed="' + (passed ? '1' : '0') + '" /></ResultSummary></TestRun>';
writeFileSync(join(reports, 'sqlite-baseline.trx'), trx('Baseline.Pass', true));
const testName = process.env.PILOT_TEST_MODE === 'wrong-failure' ? 'Unrelated.Failure' : 'SqliteVerifierFailurePilot.DeliberateFailure';
const featureTrx = trx(probe ? testName : 'Feature.Pass', !probe);
writeFileSync(join(reports, 'sqlite-features.trx'), process.env.PILOT_TEST_MODE === 'unsafe-trx'
  ? '<!DOCTYPE TestRun [<!ENTITY x "unsafe">]>' + featureTrx : featureTrx);
writeFileSync(join(reports, 'sqlite.json'), JSON.stringify({ attempted: true, build: 'success', baseline: { total: 1 }, ...(probe ? {} : { features: { total: 1 } }) }));
if (probe || process.env.PILOT_TEST_MODE === 'clean-failure') process.exit(1);
`;

function fixture() {
  const path = mkdtempSync(join(tmpdir(), 'sqlite-pilot-test-'));
  const put = (name, text) => { const target = join(path, name); mkdirSync(dirname(target), { recursive: true }); writeFileSync(target, text); };
  mkdirSync(join(path, '.github/ai'), { recursive: true });
  copyFileSync(pilot, join(path, '.github/ai/sqlite-pilot.mjs'));
  put('scripts/validation.mjs', `export { compilerResults, descendants, files, readXml } from ${JSON.stringify(validation)};\n`);
  put('.github/ai/verify-sqlite.mjs', verifier);
  put('src/DSharp.Runtime/Existing.cs', 'namespace DSharp.Runtime;');
  put('src/DSharp.Runtime.Sqlite/Existing.cs', 'namespace DSharp.Runtime.Sqlite;');
  put('tests/sqlite-features/Existing.cs', 'public sealed class ExistingSqliteFeature { }');
  return path;
}

for (const nested of [false, true]) {
  test(`pilot rejects a symlinked report ${nested ? 'ancestor' : 'directory'} before touching output`, async () => {
    const path = fixture();
    try {
      const real = join(path, 'real');
      const target = nested ? join(real, 'child') : real;
      mkdirSync(target, { recursive: true });
      writeFileSync(join(target, 'pilot.json'), '{"untouched":true}');
      const link = join(path, 'linked');
      symlinkSync(real, link);
      const reports = nested ? join(link, 'child') : link;
      const observation = join(path, 'observed.jsonl');
      const result = await new Promise((resolveRun, rejectRun) => {
        const child = spawn(process.execPath, [join(path, '.github/ai/sqlite-pilot.mjs'), reports], {
          cwd: path, env: { ...process.env, PILOT_TEST_MODE: 'normal', PILOT_OBSERVATION_FILE: observation }
        });
        child.stderr.resume();
        child.once('error', rejectRun);
        child.once('close', status => resolveRun(status));
      });
      assert.notEqual(result, 0);
      assert.equal(readFileSync(join(target, 'pilot.json'), 'utf8'), '{"untouched":true}');
      assert.equal(existsSync(observation), false);
    } finally { rmSync(path, { recursive: true, force: true }); }
  });
}

async function run(path, mode) {
  const reports = join(path, 'reports'), observation = join(path, 'observed.jsonl');
  mkdirSync(reports, { recursive: true });
  writeFileSync(join(reports, 'pilot.json'), '{"stale":true}');
  const result = await new Promise((resolveRun, rejectRun) => {
    const child = spawn(process.execPath, [join(path, '.github/ai/sqlite-pilot.mjs'), reports], {
      cwd: path, env: { ...process.env, PILOT_TEST_MODE: mode, PILOT_OBSERVATION_FILE: observation }
    });
    let stderr = '';
    child.stderr.setEncoding('utf8');
    child.stderr.on('data', chunk => { stderr += chunk; });
    child.once('error', rejectRun);
    child.once('close', status => resolveRun({ status, stderr }));
  });
  const calls = existsSync(observation) ? readFileSync(observation, 'utf8').trim().split('\n').map(JSON.parse) : [];
  return { result, reports, calls };
}

test('pilot requires its named failure and a clean second run', async () => {
  const path = fixture();
  try {
    const { result, reports, calls } = await run(path, 'normal');
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(calls.map(item => item.probe), [true, false]);
    assert.equal(calls[0].candidate, calls[1].candidate);
    assert.equal(existsSync(calls[0].candidate), false);
    const summary = JSON.parse(readFileSync(join(reports, 'pilot.json'), 'utf8'));
    assert.equal(summary.intentionalTest, 'SqliteVerifierFailurePilot.DeliberateFailure');
    assert.equal(summary.outcome, 'Failed');
    assert.equal(summary.cleanExitCode, 0);
  } finally { rmSync(path, { recursive: true, force: true }); }
});

for (const mode of ['infrastructure', 'wrong-failure', 'unsafe-trx', 'clean-failure']) {
  test(`pilot fails closed for ${mode}`, async () => {
    const path = fixture();
    try {
      const { result, reports, calls } = await run(path, mode);
      assert.notEqual(result.status, 0);
      assert.equal(existsSync(join(reports, 'pilot.json')), false);
      assert(calls.length > 0);
      assert.equal(existsSync(calls[0].candidate), false);
      assert.deepEqual(calls.map(item => item.probe), mode === 'clean-failure' ? [true, false] : [true]);
    } finally { rmSync(path, { recursive: true, force: true }); }
  });
}
