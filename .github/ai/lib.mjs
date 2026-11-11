import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';

export const sha = value => typeof value === 'string' && /^[0-9a-f]{40}$/.test(value);
export function validatePlan(plan, criteria, policy) {
  for (const key of ['affectedFiles', 'steps', 'criteria', 'assumptions', 'risks']) assert(Array.isArray(plan?.[key]), `Missing plan ${key}`);
  for (const key of ['affectedFiles', 'steps', 'assumptions', 'risks']) assert(plan[key].every(s => typeof s === 'string'), `Invalid plan ${key}`);
  assert(plan.steps.length && plan.criteria.length === criteria.length, 'Incomplete plan');
  assert(new Set(plan.criteria.map(c => c.id)).size === criteria.length, 'Duplicate plan criteria');
  for (const id of criteria) assert(plan.criteria.some(c => c.id === id && typeof c.evidence === 'string' && c.evidence.trim()), `Missing plan mapping ${id}`);
  assert(plan.affectedFiles.length > 0 && plan.affectedFiles.length <= policy.maxFiles && plan.affectedFiles.every(p => allowedPath(p, policy)), 'Plan outside approved scope');
  return plan;
}
export function parseSpec(body, sections) {
  assert.equal(typeof body, 'string');
  const contents = {};
  for (const section of sections) {
    const escaped = section.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = body.match(new RegExp(`^### ${escaped}\\r?\\n([\\s\\S]*?)(?=^### |(?![\\s\\S]))`, 'm'));
    assert(match && match[1].trim() && !/^_No response_\s*$/m.test(match[1]), `Missing ${section}`);
    contents[section] = match[1];
  }
  const criteria = [...(contents['Acceptance criteria'] || '').matchAll(/^AC([1-9]\d*):\s*\S.+/gm)].map(m => `AC${m[1]}`);
  assert(criteria.length && new Set(criteria).size === criteria.length, 'Unique AC1: criteria required');
  return criteria;
}
export function allowedPath(path, policy) {
  if (typeof path !== 'string' || path.split('/').some(p => !p || p === '..' || ['bin', 'obj', 'node_modules'].includes(p) || p.startsWith('.')) || path.includes('\\')) return false;
  // The same bounded glob patterns are passed to the publisher. **/ includes zero directories.
  return policy.allowedFiles.some(pattern => {
    const regex = pattern.split('**/').map(part => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&').replaceAll('*', '[^/]*')).join('(?:.*/)?');
    return new RegExp('^' + regex + '$').test(path);
  });
}
export function validatePatch(root, base, head, policy) {
  assert(sha(base) && sha(head), 'Invalid revision');
  execFileSync('git', ['-C', root, 'merge-base', '--is-ancestor', base, head]);
  const diff = args => execFileSync('git', ['-C', root, 'diff', '--no-ext-diff', '--no-renames', ...args, base, head]);
  const paths = diff(['--name-only', '-z']).toString().split('\0').filter(Boolean);
  assert(paths.length > 0 && paths.length <= policy.maxFiles, 'Empty or oversized diff');
  assert(diff(['--binary']).length <= policy.maxPatchBytes, 'Patch too large');
  for (const path of paths) {
    assert(!path.split('/').some(p => p === '..' || p === 'bin' || p === 'obj' || p.startsWith('.')), `Hidden/unsafe path: ${path}`);
    assert(allowedPath(path, policy), `Protected file: ${path}`);
    if (path.startsWith('tests/')) {
      const existing = execFileSync('git', ['-C', root, 'ls-tree', base, '--', path]).toString();
      assert(!existing, `Existing tests are immutable to workers: ${path}`);
    }
    const entry = execFileSync('git', ['-C', root, 'ls-tree', head, '--', path]).toString();
    assert(entry.startsWith('100644 blob '), `Deletion, symlink or executable requires human review: ${path}`);
  }
  if (paths.some(p => p.startsWith('src/'))) assert(paths.some(p => p.startsWith('tests/features/') && p.endsWith('.cs')), 'Code changes require new feature tests');
  return paths;
}
export function validateReview(report, head, criteria) {
  assert(report && Object.keys(report).sort().join(',') === 'blockers,criteria,headSha,verdict', 'Unexpected review fields');
  assert(sha(head) && report.headSha === head, 'Stale review');
  assert(['pass', 'changes_required', 'needs_decision'].includes(report.verdict), 'Invalid verdict');
  assert(Array.isArray(report.blockers) && report.blockers.every(b => b && typeof b.description === 'string' && b.description.trim() && typeof b.requiredEvidence === 'string' && b.requiredEvidence.trim()), 'Invalid blockers');
  assert(Array.isArray(report.criteria) && report.criteria.length === criteria.length, 'Missing AC evidence');
  assert(new Set(report.criteria.map(c => c.id)).size === criteria.length, 'Duplicate criteria');
  for (const id of criteria) {
    const criterion = report.criteria.find(c => c.id === id);
    assert(criterion && ['pass', 'fail'].includes(criterion.status) && typeof criterion.evidence === 'string' && criterion.evidence.trim(), `Missing evidence: ${id}`);
  }
  if (report.verdict === 'pass') assert(!report.blockers.length && report.criteria.every(c => c.status === 'pass'), 'Contradictory review');
  return report;
}
export function decide({ head, verifiedHead, ci, review, attempts, deadline }, now = Date.now()) {
  if (now >= deadline) return 'blocked';
  if (head !== verifiedHead || head !== review?.headSha) return 'blocked';
  if (review.verdict === 'needs_decision') return 'blocked';
  if (ci === 'success' && review.verdict === 'pass' && review.blockers.length === 0) return 'ready';
  // Infrastructure, malformed, missing, skipped and cancelled results never trigger code repair.
  if (!['success', 'failure'].includes(ci)) return 'blocked';
  return attempts < 2 ? 'repair' : 'blocked';
}

export function validateVerification(report, head, base, conclusion) {
  assert(report.headSha === head && report.baseSha === base && report.policy === 'pass', 'Policy failure or stale CI evidence');
  assert(['success', 'failure'].includes(report.checks) && report.checks === conclusion, 'Checks and run disagree');
  const compiler = report.compiler;
  assert(compiler?.attempted === true, 'Compiler verification never ran');
  if (conclusion === 'success') {
    assert(compiler.build === 'success', 'No successful build evidence');
    const valid = group => Number.isSafeInteger(group?.total) && group.total > 0 && group.passed === group.total && group.skipped === 0;
    assert(valid(compiler.acceptance), 'Missing/nonpassing trusted acceptance tests');
    assert(Array.isArray(report.changedFiles), 'Missing scope evidence');
    if (report.changedFiles.some(p => p.startsWith('src/'))) assert(valid(compiler.features), 'Code task requires nonzero feature test evidence');
  }
  return report;
}
