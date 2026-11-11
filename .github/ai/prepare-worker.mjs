// This file is executed from the workflow's trusted revision, before any checkout of candidate data.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
const input = event.inputs;
const root = '/tmp/gh-aw/task';
const manifest = JSON.parse(readFileSync(`${root}/manifest.json`, 'utf8'));
assert(manifest.version === 1 && manifest.repository === process.env.GITHUB_REPOSITORY);
assert(String(manifest.controllerRun) === input.controller_run);
assert(manifest.baseSha === input.base_sha && input.base_sha === process.env.GITHUB_SHA, 'Trusted base moved');
assert(input.model === manifest.model && input.model.trim(), 'Selected model differs from frozen task');
assert(Date.now() < manifest.deadline && manifest.deadline < Date.now() + 61 * 60000);
assert(createHash('sha256').update(manifest.specification).digest('hex') === manifest.specHash);
assert(new RegExp(`^ai-${manifest.controllerRun}-[0-2]-ai-(implement|review|repair)$`).test(input.run_key));
const api = async path => {
  const response = await fetch(`https://api.github.com/repos/${process.env.GITHUB_REPOSITORY}${path}`, {
    headers: { Authorization: `Bearer ${process.env.GH_TOKEN}` }, signal: AbortSignal.timeout(30000),
  });
  assert(response.ok, `Context lookup HTTP ${response.status}`);
  return response.json();
};
const controller = await api(`/actions/runs/${manifest.controllerRun}`);
assert(controller.path === '.github/workflows/ai-coordinate.yml' && controller.status === 'in_progress' && controller.run_attempt === 1 && controller.head_sha === manifest.baseSha, 'Untrusted controller');
const mode = process.argv[2];
let head = manifest.baseSha;
if (mode === 'implement') {
  assert(input.branch === manifest.branch && /^ai\/issue-[1-9][0-9]*$/.test(input.branch));
} else {
  assert(/^[0-9a-f]{40}$/.test(input.head_sha));
  head = input.head_sha;
  const prs = await api(`/pulls?state=open&head=${encodeURIComponent(process.env.GITHUB_REPOSITORY.split('/')[0] + ':' + manifest.branch)}`);
  assert(prs.length === 1 && prs[0].draft && prs[0].head.sha === head && prs[0].base.sha === manifest.baseSha, 'Stale or unexpected PR');
  assert(prs[0].head.repo.full_name === manifest.repository);
  if (mode === 'repair') assert(String(prs[0].number) === input.pr_number);
}
const git = args => execFileSync('git', args, { maxBuffer: 10485760 });
git(['fetch', '--no-tags', 'origin', head]);
// Only checkout data here: no candidate scripts, hooks, dependency install or configuration execution.
if (git(['rev-parse', 'HEAD']).toString().trim() !== head) {
  git(['-c', 'core.hooksPath=/dev/null', 'checkout', '--detach', head]);
}
assert(git(['rev-parse', 'HEAD']).toString().trim() === head);
// The candidate cannot replace repository instructions.
git(['checkout', manifest.baseSha, '--', 'AGENTS.md']);
mkdirSync(root, { recursive: true });
writeFileSync(`${root}/context.json`, JSON.stringify({ ...manifest, selectedHead: head, mode, ...input }, null, 2));
writeFileSync(`${root}/specification.md`, manifest.specification);
