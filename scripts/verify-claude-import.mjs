// Verify relative import loading without contacting an AI service or using credentials.
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const executable = process.argv[2] && resolve(process.argv[2]);
assert.ok(executable, 'Pass the Claude Code 2.1.332 executable path.');
assert.equal(execFileSync(executable, ['--version'], { encoding: 'utf8' }).trim(), '2.1.332 (Claude Code)');
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const temp = mkdtempSync(join(tmpdir(), 'dsharp-claude-import-'));

async function check(directory, marker, withImport) {
  const fixture = join(temp, `${directory.replaceAll('/', '-')}-${withImport ? 'import' : 'control'}`);
  mkdirSync(fixture);
  const agents = readFileSync(join(root, directory, 'AGENTS.md'), 'utf8');
  const claude = readFileSync(join(root, directory, 'CLAUDE.md'), 'utf8');
  assert.equal(claude, '@AGENTS.md\n');
  assert.ok(agents.includes(marker));
  writeFileSync(join(fixture, 'AGENTS.md'), agents);
  writeFileSync(join(fixture, 'CLAUDE.md'), withImport ? claude : '# Control without import\n');
  let requests = 0, sawMarker = false;
  const server = createServer((req, res) => {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      if (req.method !== 'POST' || !req.url?.startsWith('/v1/messages')) {
        res.writeHead(404); res.end(); return;
      }
      requests++;
      sawMarker ||= body.includes(marker);
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ id: 'msg_local', type: 'message', role: 'assistant', model: 'test',
        content: [{ type: 'text', text: 'OK' }], stop_reason: 'end_turn', stop_sequence: null,
        usage: { input_tokens: 10, output_tokens: 1 } }));
    });
  });
  await new Promise((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', ok); });
  try {
    const address = `http://127.0.0.1:${server.address().port}`;
    // Explicit environment excludes real credentials and sends HTTP(S) through
    // the local mock; the mock never forwards requests.
    const env = { PATH: process.env.PATH, CLAUDE_CONFIG_DIR: join(temp, 'config'),
      ANTHROPIC_BASE_URL: address, ANTHROPIC_API_KEY: 'local-test-only',
      CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC: '1', DISABLE_TELEMETRY: '1',
      HTTP_PROXY: address, HTTPS_PROXY: address, ALL_PROXY: address, NO_PROXY: '127.0.0.1,localhost' };
    const result = await new Promise((ok, fail) => {
      const child = spawn(executable, ['--print', '--strict-mcp-config', '--setting-sources', 'project',
        '--model', 'test', '--max-turns', '1', 'Reply OK'], { cwd: fixture, env, stdio: ['ignore', 'pipe', 'pipe'] });
      let stdout = '', stderr = '';
      child.stdout.on('data', chunk => { stdout += chunk; });
      child.stderr.on('data', chunk => { stderr += chunk; });
      const timeout = setTimeout(() => child.kill('SIGKILL'), 30000);
      child.on('error', error => { clearTimeout(timeout); fail(error); });
      child.on('close', code => { clearTimeout(timeout); ok({ code, stdout, stderr }); });
    });
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /OK/);
    assert.ok(requests > 0, 'Claude did not call the local mock.');
    assert.equal(sawMarker, withImport, `${directory}: unexpected import behavior`);
  } finally {
    await new Promise(ok => server.close(ok));
  }
}

try {
  for (const [directory, marker] of [
    ['examples/inventory-app', 'This example uses the repository compiler'],
    ['templates/application', 'This is a scaffold source']
  ]) {
    await check(directory, marker, false);
    await check(directory, marker, true);
  }
  console.log('Claude Code 2.1.332 relative imports passed for both files; controls omitted them.');
} finally {
  rmSync(temp, { recursive: true, force: true });
}
