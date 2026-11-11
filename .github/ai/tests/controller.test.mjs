import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
for (const scenario of ['success', 'stale', 'exhausted']) {
  test(`controller integration: ${scenario}`, () => {
    const dir = mkdtempSync(join(tmpdir(), 'ai-controller-test-'));
    try {
      writeFileSync(join(dir, 'manifest.json'), JSON.stringify({ controllerRun: 1, repository: 'test/repo', actor: 'maintainer', model: 'test-model', baseSha: 'b'.repeat(40), branch: 'ai/issue-1', criteria: ['AC1'], deadline: Date.now() + 60000 }));
      const result = spawnSync(process.execPath, ['--import', fileURLToPath(new URL('./mock-github.mjs', import.meta.url)), fileURLToPath(new URL('../coordinator.ts', import.meta.url)), 'coordinate'], {
        cwd: dir, encoding: 'utf8', timeout: 10000,
        env: { ...process.env, GH_TOKEN: 'test-placeholder', GITHUB_REPOSITORY: 'test/repo', GITHUB_RUN_ID: '1', GITHUB_STEP_SUMMARY: join(dir, 'summary.md'), SCENARIO: scenario },
      });
      const state = JSON.parse(readFileSync(join(dir, 'state.json')));
      const trace = readFileSync(join(dir, 'trace.jsonl'), 'utf8').trim().split('\n').map(JSON.parse);
      if (scenario === 'success') {
        assert.equal(result.status, 0, result.stderr);
        assert.equal(state.phase, 'READY');
        assert.equal(trace.filter(t => t.ready).length, 1);
        assert.deepEqual(trace.filter(t => t.dispatch).map(t => t.dispatch), ['ai-implement.lock.yml', 'ai-verify.yml', 'ai-review.lock.yml']);
      } else {
        assert.notEqual(result.status, 0);
        assert.equal(state.phase, 'BLOCKED');
        assert.equal(trace.filter(t => t.ready).length, 0);
        assert.equal(trace.filter(t => t.dispatch === 'ai-repair.lock.yml').length, scenario === 'exhausted' ? 2 : 0);
        if (scenario === 'exhausted') {
          assert.equal(trace.filter(t => t.dispatch === 'ai-verify.yml').length, 3);
          assert.equal(trace.filter(t => t.dispatch === 'ai-review.lock.yml').length, 3);
        }
      }
    } finally { rmSync(dir, { recursive: true, force: true }); }
  });
}
