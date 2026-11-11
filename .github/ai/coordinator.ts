import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, appendFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import { parseSpec, validatePlan, validateReview, decide, sha, validateVerification } from './lib.mjs';

const policy = JSON.parse(readFileSync(new URL('./policy.json', import.meta.url), 'utf8'));
const repo = process.env.GITHUB_REPOSITORY!;
const [owner, name] = (repo || '').split('/');
const token = process.env.GH_TOKEN;
const runId = Number(process.env.GITHUB_RUN_ID);
assert(owner && name && token && Number.isSafeInteger(runId));
const apiRoot = `https://api.github.com/repos/${owner}/${name}`;
async function api(path: string, method = 'GET', body?: unknown): Promise<any> {
  const result = await fetch(`${apiRoot}${path}`, {
    method, headers: { Authorization: `Bearer ${path.endsWith('/dispatches') ? process.env.ROUTER_TOKEN || token : token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    signal: AbortSignal.timeout(30000),
  });
  if (!result.ok) throw Object.assign(new Error(`GitHub ${method} ${path}: HTTP ${result.status}`), { status: result.status });
  return result.status === 204 ? null : result.json();
}
const hash = (text: string | Buffer) => createHash('sha256').update(text).digest('hex');
const output = (key: string, value: string) => appendFileSync(process.env.GITHUB_OUTPUT!, `${key}=${value}\n`);

async function preflight() {
  assert(process.env.GITHUB_REF === `refs/heads/${policy.baseBranch}`, 'Launch only on main');
  assert(process.env.GITHUB_RUN_ATTEMPT === '1', 'Start a new run instead of rerunning old state');
  const issueNumber = Number(process.env.ISSUE_NUMBER);
  assert(Number.isSafeInteger(issueNumber) && issueNumber > 0, 'Invalid issue number');
  assert(process.env.AI_MODEL?.trim(), 'Configure repository variable AI_MODEL');
  const permission = await api(`/collaborators/${encodeURIComponent(process.env.GITHUB_ACTOR!)}/permission`);
  assert(['admin', 'maintain', 'write'].includes(permission.permission), 'Maintainer authorization required');
  const issue = await api(`/issues/${issueNumber}`);
  assert(!issue.pull_request && issue.state === 'open', 'An open issue is required');
  assert(issue.labels.some((label: any) => label.name === 'ai:approved'), 'Maintainer must add ai:approved');
  assert(policy.mode === 'compiler-code' && policy.version === 2, 'Unapproved executable policy');
  for (const file of ['global.json', 'DSharp.slnx', 'tests/DSharp.Compiler.Tests/CompilerTests.cs', '.github/ai/verify-compiler.mjs']) readFileSync(file);
  const criteria = parseSpec(issue.body, policy.requiredSections);
  const baseSha = (await api(`/git/ref/heads/${policy.baseBranch}`)).object.sha;
  assert(sha(baseSha) && baseSha === process.env.GITHUB_SHA, 'Main moved; start a fresh run');
  const branch = `ai/issue-${issueNumber}`;
  const existing = await api(`/pulls?state=all&head=${encodeURIComponent(owner + ':' + branch)}&per_page=100`);
  assert(existing.length === 0, 'A PR already exists for this issue; do not duplicate it');
  try {
    await api(`/git/ref/heads/${branch}`);
    throw new Error('Task branch already exists; human reconciliation required');
  } catch (error: any) { if (error.status !== 404) throw error; }
  const instructions = ['AGENTS.md', 'docs/ARCHITECTURE.md', 'docs/LANGUAGE.md', 'docs/COMPILER_BOOTSTRAP.md', 'docs/features/DSharp_Syntax_Reference.md', 'docs/features/DSharp_Implementation_Instructions.md', '.github/ai/policy.json', 'global.json', 'package.json', 'package-lock.json'].map(file => ({ file, sha256: hash(readFileSync(file)) }));
  const manifest = {
    version: 1, repository: repo, controllerRun: runId, issueNumber, branch, baseSha,
    specification: issue.body, specHash: hash(issue.body), criteria, instructions,
    deadline: Date.now() + policy.deadlineMinutes * 60000,
    actor: process.env.GITHUB_ACTOR, model: process.env.AI_MODEL, phase: 'PREFLIGHT', repairCount: 0,
  };
  writeFileSync('manifest.json', JSON.stringify(manifest, null, 2));
  output('base', baseSha);
  output('branch', branch);
}

async function coordinate() {
  const manifest = JSON.parse(readFileSync('manifest.json', 'utf8'));
  assert(manifest.controllerRun === runId && manifest.repository === repo && sha(manifest.baseSha));
  const launched: number[] = [];
  const evidence: any[] = [];
  let pr: any;
  const checkDeadline = () => assert(Date.now() < manifest.deadline - 15000, 'Overall 60-minute deadline exhausted');
  const delay = () => new Promise(resolve => setTimeout(resolve, 5000));
  const save = () => writeFileSync('state.json', JSON.stringify({ ...manifest, evidence }, null, 2));
  async function dispatch(workflow: string, attempt: number, extra: Record<string, string> = {}) {
    checkDeadline();
    const key = `ai-${runId}-${attempt}-${workflow.replace(/\..*/, '')}`;
    const inputs = { controller_run: String(runId), run_key: key, base_sha: manifest.baseSha, ...extra,
      ...(workflow.endsWith('.lock.yml') ? { model: manifest.model, aw_context: JSON.stringify({ repo, actor: manifest.actor, command_name: 'ai-coordinate', ...(workflow === 'ai-repair.lock.yml' ? { item_type: 'pull_request', item_number: Number(extra.pr_number), pr_head_sha: extra.head_sha } : {}) }) } : {}) };
    const started = new Date().toISOString();
    await api(`/actions/workflows/${workflow}/dispatches`, 'POST', { ref: policy.baseBranch, inputs });
    let run: any;
    while (!run) {
      checkDeadline();
      const runs = await api(`/actions/workflows/${workflow}/runs?event=workflow_dispatch&per_page=100&created=${encodeURIComponent('>=' + started)}`);
      const matches = runs.workflow_runs.filter((r: any) => r.display_title === key);
      assert(matches.length <= 1, 'Ambiguous worker run');
      run = matches[0];
      if (!run) await delay();
    }
    launched.push(run.id);
    assert(run.head_sha === manifest.baseSha && run.run_attempt === 1 && run.event === 'workflow_dispatch', 'Wrong workflow revision or rerun');
    evidence.push({ workflow, attempt, runId: run.id, headSha: extra.head_sha ?? manifest.baseSha });
    save();
    while (run.status !== 'completed') {
      checkDeadline();
      await delay();
      run = await api(`/actions/runs/${run.id}`);
      assert(run.run_attempt === 1, 'Worker was rerun; state is no longer valid');
    }
    evidence.at(-1).conclusion = run.conclusion;
    save();
    return run;
  }
  async function artifact(run: any, artifactName: string, filename: string) {
    const artifacts = await api(`/actions/runs/${run.id}/artifacts?per_page=100`);
    const matches = artifacts.artifacts.filter((a: any) => a.name === artifactName && !a.expired);
    assert(matches.length === 1, `Missing/ambiguous ${artifactName}`);
    assert(matches[0].size_in_bytes < 1048576, 'Artifact exceeds size limit');
    const response = await fetch(`${apiRoot}/actions/artifacts/${matches[0].id}/zip`, {
      headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(30000),
    });
    assert(response.ok, 'Artifact download failed');
    const bytes = Buffer.from(await response.arrayBuffer());
    assert(bytes.length < 1048576, 'Artifact exceeds download limit');
    const temp = mkdtempSync(join(tmpdir(), 'ai-report-'));
    try {
      writeFileSync(join(temp, 'report.zip'), bytes);
      // Read only the named JSON member; never extract or execute artifact contents.
      const json = execFileSync('unzip', ['-p', join(temp, 'report.zip'), filename], { maxBuffer: 131072 });
      return JSON.parse(json.toString());
    } finally { rmSync(temp, { recursive: true, force: true }); }
  }
  async function getPR() {
    const list = await api(`/pulls?state=open&head=${encodeURIComponent(owner + ':' + manifest.branch)}&per_page=100`);
    assert(list.length === 1, 'Expected exactly one open task PR');
    const current = await api(`/pulls/${list[0].number}`);
    assert(current.head.repo.full_name === repo && current.head.ref === manifest.branch && current.base.ref === policy.baseBranch, 'Unexpected PR destination');
    assert(current.base.sha === manifest.baseSha && current.draft && !current.merged, 'Base moved or PR was changed by a human');
    return current;
  }
  try {
    manifest.phase = 'IMPLEMENTING'; save();
    const implementation = await dispatch('ai-implement.lock.yml', 0, { branch: manifest.branch });
    assert(implementation.conclusion === 'success', 'Implementation/publishing failed; inspect worker logs');
    validatePlan(await artifact(implementation, `plan-${implementation.display_title}`, 'plan.json'), manifest.criteria, policy);
    pr = await getPR();
    for (let attempt = 0; attempt <= policy.maxRepairs; attempt++) {
      checkDeadline();
      pr = await getPR();
      const head = pr.head.sha;
      manifest.phase = 'CHECKING'; manifest.repairCount = attempt; manifest.currentHead = head; save();
      // Fresh runners and fresh contexts; CI is never the worker's local result.
      const ci = await dispatch('ai-verify.yml', attempt, { head_sha: head });
      if (ci.conclusion !== 'success' && ci.conclusion !== 'failure') throw new Error('CI infrastructure/cancellation failure');
      const verification = await artifact(ci, `verification-${head}`, 'verification.json');
      validateVerification(verification, head, manifest.baseSha, ci.conclusion);
      const reviewed = await dispatch('ai-review.lock.yml', attempt, { head_sha: head, ci_run: String(ci.id) });
      assert(reviewed.conclusion === 'success', 'Independent reviewer failed');
      const review = validateReview(await artifact(reviewed, `review-${head}`, 'review.json'), head, manifest.criteria);
      pr = await getPR();
      const decision = decide({ head: pr.head.sha, verifiedHead: head, ci: ci.conclusion, review, attempts: attempt, deadline: manifest.deadline });
      evidence.push({ headSha: head, ciRun: ci.id, reviewRun: reviewed.id, review, decision }); save();
      if (decision === 'ready') {
        checkDeadline();
        // Last freshness check immediately before changing PR readiness.
        assert((await getPR()).head.sha === head, 'Head moved before readiness');
        const response = await fetch('https://api.github.com/graphql', {
          method: 'POST', headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
          body: JSON.stringify({ query: 'mutation($id:ID!){markPullRequestReadyForReview(input:{pullRequestId:$id}){pullRequest{isDraft}}}', variables: { id: pr.node_id } }),
          signal: AbortSignal.timeout(30000),
        });
        const result: any = await response.json();
        assert(response.ok && !result.errors && result.data?.markPullRequestReadyForReview.pullRequest.isDraft === false, 'Could not mark PR ready');
        // A concurrent write is a blocker; restore draft rather than retaining stale readiness.
        const latest = await api(`/pulls/${pr.number}`);
        if (latest.head.sha !== head) {
          await fetch('https://api.github.com/graphql', { method: 'POST', headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ query: 'mutation($id:ID!){convertPullRequestToDraft(input:{pullRequestId:$id}){pullRequest{isDraft}}}', variables: { id: pr.node_id } }) });
          throw new Error('Concurrent head change; readiness withdrawn');
        }
        manifest.phase = 'READY'; save();
        appendFileSync(process.env.GITHUB_STEP_SUMMARY!, `PR #${pr.number} ready for human review.\n\nRevision: ${head}\n\nCI run: ${ci.id}; independent review: ${reviewed.id}; repairs: ${attempt}.\n\nPinned compiler restore/build, trusted acceptance and feature tests verified. No agent runtime is claimed. No merge or deployment performed.\n`);
        return;
      }
      assert(decision === 'repair', 'Blocked: decision needed, stale evidence, deadline, or repair budget exhausted');
      manifest.phase = 'REPAIRING'; save();
      const repaired = await dispatch('ai-repair.lock.yml', attempt + 1, { head_sha: head, pr_number: String(pr.number), ci_run: String(ci.id), review_run: String(reviewed.id) });
      assert(repaired.conclusion === 'success', 'Repair failed');
      validatePlan(await artifact(repaired, `plan-${repaired.display_title}`, 'plan.json'), manifest.criteria, policy);
      assert((await getPR()).head.sha !== head, 'Repair produced no new revision');
    }
    throw new Error('Repair budget exhausted');
  } catch (error) {
    manifest.phase = 'BLOCKED'; save();
    for (const id of launched) {
      const run = await api(`/actions/runs/${id}`).catch(() => null);
      if (run && run.status !== 'completed') await api(`/actions/runs/${id}/cancel`, 'POST').catch(() => {});
    }
    appendFileSync(process.env.GITHUB_STEP_SUMMARY!, `Task BLOCKED. PR remains in draft. Inspect state.json and the linked worker runs.\n\n${String(error)}\n`);
    throw error;
  }
}

if (process.argv[2] === 'preflight') await preflight();
else if (process.argv[2] === 'coordinate') await coordinate();
else throw new Error('Expected preflight or coordinate');
