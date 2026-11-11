import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseSpec, validatePatch, validateReview, decide } from '../lib.mjs';
const head = 'a'.repeat(40);
const report = () => ({ headSha: head, verdict: 'pass', blockers: [], criteria: [{ id: 'AC1', status: 'pass', evidence: 'docs/features/example.md: explicit shutdown contract' }] });
test('GitHub issue form sections accept blank lines and reject missing answers', () => {
  assert.deepEqual(parseSpec('### Goal\n\nExample\n\n### Acceptance criteria\n\nAC1: observable outcome\n', ['Goal', 'Acceptance criteria']), ['AC1']);
  assert.throws(() => parseSpec('### Goal\n\n_No response_\nAC1: example', ['Goal']));
  assert.throws(() => parseSpec('### Goal\nExample', ['Goal']));
});
test('review must be current, complete and internally consistent', () => {
  validateReview(report(), head, ['AC1']);
  assert.throws(() => validateReview(report(), 'b'.repeat(40), ['AC1']));
  assert.throws(() => validateReview({ ...report(), criteria: [] }, head, ['AC1']));
  assert.throws(() => validateReview({ ...report(), blockers: [{ description: 'Bug', requiredEvidence: 'Regression test' }] }, head, ['AC1']));
  assert.throws(() => validateReview({}, head, ['AC1']));
});
test('readiness rejects stale, skipped and cancelled checks; repairs are bounded', () => {
  const state = { head, verifiedHead: head, ci: 'success', review: report(), attempts: 0, deadline: 100 };
  assert.equal(decide(state, 1), 'ready');
  assert.equal(decide({ ...state, head: 'b'.repeat(40) }, 1), 'blocked');
  assert.equal(decide(state, 100), 'blocked');
  const failing = { ...state, review: { ...report(), verdict: 'changes_required' }, ci: 'failure' };
  assert.equal(decide(failing, 1), 'repair');
  assert.equal(decide({ ...failing, attempts: 2 }, 1), 'blocked');
  for (const ci of ['skipped', 'cancelled', 'timed_out', undefined]) assert.equal(decide({ ...failing, ci }, 1), 'blocked');
  assert.equal(decide({ ...failing, review: { ...report(), verdict: 'needs_decision' } }, 1), 'blocked');
});
test('real Git diffs reject protected files, symlinks, deletions and oversize patches', () => {
  const root = mkdtempSync(join(tmpdir(), 'ai-policy-test-'));
  const git = args => execFileSync('git', ['-C', root, ...args]).toString().trim();
  const commit = () => { git(['add', '.']); git(['-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'fixture']); return git(['rev-parse', 'HEAD']); };
  try {
    git(['init', '-q']); writeFileSync(join(root, 'README.md'), 'base'); const base = commit();
    mkdirSync(join(root, 'docs/features'), { recursive: true }); writeFileSync(join(root, 'docs/features/ok.md'), 'valid');
    let candidate = commit();
    const policy = { allowedPrefixes: ['docs/features/'], maxFiles: 20, maxPatchBytes: 131072 };
    assert.deepEqual(validatePatch(root, base, candidate, policy), ['docs/features/ok.md']);
    assert.throws(() => validatePatch(root, base, candidate, { ...policy, maxPatchBytes: 1 }));
    assert.throws(() => validatePatch(root, base, candidate, { ...policy, maxFiles: 0 }));
    writeFileSync(join(root, 'README.md'), 'weakened checker'); candidate = commit();
    assert.throws(() => validatePatch(root, base, candidate, policy));
    git(['reset', '--hard', base]); mkdirSync(join(root, 'docs/features'), { recursive: true });
    git(['-c', 'core.symlinks=true', 'update-index', '--add', '--cacheinfo', `120000,${git(['hash-object', '-w', '--stdin'])},docs/features/link.md`]);
    git(['-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'symlink']);
    assert.throws(() => validatePatch(root, base, git(['rev-parse', 'HEAD']), policy));
    git(['reset', '--hard', base]); mkdirSync(join(root, 'docs/features'), { recursive: true }); writeFileSync(join(root, 'docs/features/delete.md'), 'existing'); const withDoc = commit();
    git(['rm', 'docs/features/delete.md']); candidate = commit();
    assert.throws(() => validatePatch(root, withDoc, candidate, policy));
  } finally { rmSync(root, { recursive: true, force: true }); }
});
