import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, readFileSync, mkdirSync, rmSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseSpec, validatePatch, validateReview, decide, allowedPath, validatePlan, validateVerification } from '../lib.mjs';
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
    const policy = { allowedFiles: ['docs/features/**/*.md'], maxFiles: 20, maxPatchBytes: 131072 };
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

const codePolicy = JSON.parse(readFileSync(new URL('../policy.json', import.meta.url)));
test('publisher and trusted policy share exact bounded implementation globs', () => {
  for (const name of ['ai-implement.md', 'ai-repair.md']) {
    const text = readFileSync(new URL(`../../workflows/${name}`, import.meta.url), 'utf8');
    const line = text.split('\n').find(line => line.includes('allowed-files:'));
    assert.deepEqual(JSON.parse(line.split('allowed-files: ')[1]), codePolicy.allowedFiles);
  }
  for (const path of ['src/DSharp.Compiler/Lexer.cs', 'src/DSharp.Compiler/Parsing/Parser.cs', 'src/DSharp.Cli/Program.cs', 'tests/features/Numbers.cs', 'examples/demo.ds', 'docs/features/implementation/status.md']) assert(allowedPath(path, codePolicy), path);
  for (const path of ['package.json', 'package-lock.json', 'src/DSharp.Compiler/node_modules/ignored.cs', 'AGENTS.md', 'docs/features/DSharp_Syntax_Reference.md', 'docs/features/DSharp_Implementation_Instructions.md', '.github/workflows/ci.yml', 'Directory.Build.props', 'src/DSharp.Compiler/DSharp.Compiler.csproj', 'src/DSharp.Compiler/packages.lock.json', 'src/DSharp.Compiler/.hidden/evil.cs', 'src/DSharp.Compiler/../evil.cs', 'tests/DSharp.Compiler.Tests/CompilerTests.cs', 'scripts/verify-compiler.sh', 'src/Other/Bad.cs']) assert(!allowedPath(path, codePolicy), path);
});
test('code pilot accepts implementation plus new tests and rejects workflow/manifest/old-test edits', () => {
  const root = mkdtempSync(join(tmpdir(), 'ai-code-policy-'));
  const git = args => execFileSync('git', ['-C', root, ...args]).toString().trim();
  const put = (path, text) => { mkdirSync(join(root, path, '..'), {recursive:true}); writeFileSync(join(root, path), text); };
  const commit = () => { git(['add','.']); git(['-c','user.name=Test','-c','user.email=test@example.invalid','commit','-qm','fixture']); return git(['rev-parse','HEAD']); };
  try {
    git(['init','-q']); put('tests/features/Existing.cs', '// existing'); put('README.md','base'); const base = commit();
    put('src/DSharp.Compiler/New.cs','// implementation');
    const noTest = commit(); assert.throws(() => validatePatch(root,base,noTest,codePolicy), /new feature tests/);
    put('tests/features/New.cs','// test'); const positive = commit();
    assert.equal(validatePatch(root,base,positive,codePolicy).length,2);
    for (const path of ['package.json', 'package-lock.json', '.github/workflows/ci.yml', 'src/DSharp.Compiler/packages.lock.json', 'src/DSharp.Compiler/DSharp.Compiler.csproj', 'tests/features/Existing.cs']) {
      git(['reset','--hard',positive]); put(path,'forbidden'); const bad = commit();
      assert.throws(() => validatePatch(root,base,bad,codePolicy));
    }
  } finally { rmSync(root,{recursive:true,force:true}); }
});
const verified = () => ({headSha:head,baseSha:'b'.repeat(40),policy:'pass',checks:'success',changedFiles:['src/DSharp.Compiler/New.cs','tests/features/New.cs'],compiler:{attempted:true,build:'success',acceptance:{total:18,passed:18,skipped:0},features:{total:1,passed:1,skipped:0}}});
test('readiness requires actual current build and nonzero acceptance and feature test evidence', () => {
  validateVerification(verified(),head,'b'.repeat(40),'success');
  for (const mutate of [r=>r.compiler.build='failure', r=>r.compiler.acceptance.total=0, r=>r.compiler.features.total=0, r=>r.compiler.acceptance.skipped=1, r=>r.compiler.features.passed=0, r=>r.headSha='c'.repeat(40), r=>r.policy='blocked', r=>delete r.compiler]) {
    const report=verified(); mutate(report); assert.throws(()=>validateVerification(report,head,'b'.repeat(40),'success'));
  }
});
