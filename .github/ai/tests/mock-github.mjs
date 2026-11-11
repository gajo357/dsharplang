// Imported only by controller integration tests in a subprocess. No network calls occur.
import assert from 'node:assert/strict';
import { appendFileSync, readFileSync } from 'node:fs';
const manifest = JSON.parse(readFileSync('manifest.json', 'utf8'));
let head = 'a'.repeat(40);
let ready = false;
let current;
const runs = new Map();
let serial = 100;
const json = value => new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
const pr = () => ({ number: 7, node_id: 'PR_7', draft: !ready, merged: false, head: { sha: head, ref: manifest.branch, repo: { full_name: manifest.repository } }, base: { sha: manifest.baseSha, ref: 'main' } });
globalThis.fetch = async (url, options = {}) => {
  const route = new URL(url).pathname.replace('/repos/test/repo', '');
  const trace = value => appendFileSync('trace.jsonl', JSON.stringify(value) + '\n');
  if (route.endsWith('/dispatches')) {
    const body = JSON.parse(options.body);
    const workflow = route.split('/')[3];
    if (workflow.endsWith('.lock.yml')) {
      assert.equal(body.inputs.model, manifest.model);
      assert.equal(JSON.parse(body.inputs.aw_context).actor, manifest.actor);
    }
    current = { id: ++serial, head_sha: manifest.baseSha, run_attempt: 1, event: 'workflow_dispatch', status: 'completed', conclusion: 'success', display_title: body.inputs.run_key, workflow, inputs: body.inputs };
    if (workflow === 'ai-verify.yml' && process.env.SCENARIO === 'build-failure') current.conclusion = 'failure';
    if (workflow === 'ai-repair.lock.yml') {
      const context = JSON.parse(body.inputs.aw_context);
      assert(context.item_number === 7 && context.pr_head_sha === head);
      head = serial.toString(16).padStart(40, '0');
    }
    runs.set(current.id, current); trace({ dispatch: workflow, head, inputs: body.inputs });
    return new Response(null, { status: 204 });
  }
  if (/\/actions\/workflows\/[^/]+\/runs$/.test(route)) return json({ workflow_runs: [current] });
  if (/\/actions\/runs\/\d+\/artifacts$/.test(route)) {
    const run = runs.get(Number(route.split('/')[3]));
    const name = ['ai-implement.lock.yml', 'ai-repair.lock.yml'].includes(run.workflow) ? `plan-${run.display_title}` : `${run.workflow === 'ai-verify.yml' ? 'verification' : 'review'}-${run.inputs.head_sha}`;
    return json({ artifacts: [{ id: run.id, name, expired: false, size_in_bytes: 100 }] });
  }
  if (/\/actions\/artifacts\/\d+\/zip$/.test(route)) {
    const run = runs.get(Number(route.split('/')[3]));
    const isPlan = ['ai-implement.lock.yml', 'ai-repair.lock.yml'].includes(run.workflow);
    const isReview = run.workflow === 'ai-review.lock.yml';
    const reportHead = process.env.SCENARIO === 'stale' && isReview ? 'f'.repeat(40) : run.inputs.head_sha;
    const fail = process.env.SCENARIO === 'exhausted';
    const report = isPlan ? { affectedFiles: ['src/DSharp.Compiler/New.cs', 'tests/features/New.cs'], steps: ['Implement and test frozen slice'], criteria: [{id:'AC1', evidence:'Documented behavior'}], assumptions: [], risks: [] } : isReview
      ? { headSha: reportHead, verdict: fail ? 'changes_required' : 'pass', blockers: fail ? [{ description: 'Missing behavior', requiredEvidence: 'Demonstrate AC1' }] : [], criteria: [{ id: 'AC1', status: fail ? 'fail' : 'pass', evidence: 'docs/features/example.md' }] }
      : { headSha: process.env.SCENARIO === 'stale-verification' ? 'f'.repeat(40) : reportHead, baseSha: manifest.baseSha, policy: process.env.SCENARIO === 'forbidden' ? 'blocked' : 'pass', checks: process.env.SCENARIO === 'build-failure' ? 'failure' : 'success', changedFiles: ['src/DSharp.Compiler/New.cs', 'tests/features/New.cs'], compiler: {attempted:true,build:process.env.SCENARIO === 'build-failure' ? 'failure' : 'success',acceptance:{total:18,passed:18,skipped:0},features:{total:process.env.SCENARIO === 'zero-tests' ? 0 : 1,passed:1,skipped:0}} };
    // A minimal, real ZIP with one uncompressed JSON member.
    const data = Buffer.from(JSON.stringify(report));
    const filename = Buffer.from(isPlan ? 'plan.json' : isReview ? 'review.json' : 'verification.json');
    let crc = 0xffffffff;
    for (const byte of data) { crc ^= byte; for (let j = 0; j < 8; j++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0); }
    crc = (crc ^ 0xffffffff) >>> 0;
    const local = Buffer.alloc(30); local.writeUInt32LE(0x04034b50); local.writeUInt16LE(20, 4); local.writeUInt32LE(crc, 14); local.writeUInt32LE(data.length, 18); local.writeUInt32LE(data.length, 22); local.writeUInt16LE(filename.length, 26);
    const central = Buffer.alloc(46); central.writeUInt32LE(0x02014b50); central.writeUInt16LE(20, 4); central.writeUInt16LE(20, 6); central.writeUInt32LE(crc, 16); central.writeUInt32LE(data.length, 20); central.writeUInt32LE(data.length, 24); central.writeUInt16LE(filename.length, 28);
    const end = Buffer.alloc(22); end.writeUInt32LE(0x06054b50); end.writeUInt16LE(1, 8); end.writeUInt16LE(1, 10); end.writeUInt32LE(central.length + filename.length, 12); end.writeUInt32LE(local.length + filename.length + data.length, 16);
    return new Response(Buffer.concat([local, filename, data, central, filename, end]));
  }
  if (/\/actions\/runs\/\d+$/.test(route)) return json(runs.get(Number(route.split('/')[3])));
  if (route === '/pulls') return json([pr()]);
  if (route === '/pulls/7') return json(pr());
  if (route === '/graphql') {
    ready = true; trace({ ready: head });
    return json({ data: { markPullRequestReadyForReview: { pullRequest: { isDraft: false } } } });
  }
  throw new Error(`Unexpected GitHub API call: ${route}`);
};
