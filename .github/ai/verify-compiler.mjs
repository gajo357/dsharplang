// Trusted host orchestration. Candidate code runs only in an offline container.
import assert from 'node:assert/strict';
import { copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { compilerResults, files } from '../../scripts/validation.mjs';
import { stagePreludeResource } from './prelude-resource.mjs';

const trusted = fileURLToPath(new URL('../..', import.meta.url));
const candidate = resolve(process.argv[2]);
const reports = resolve(process.argv[3]);
mkdirSync(reports, { recursive: true });
const evidence = { attempted: true, build: 'failure' };
function run(command, args, options = {}) {
  const result = spawnSync(command, args, { stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  assert(result.status === 0, `${command} failed (${result.status ?? result.signal})`);
  return result;
}
function copy(source, destination) { mkdirSync(dirname(destination), { recursive: true }); copyFileSync(source, destination); }
let config;
try {
  const cache = resolve(process.env.AI_NUGET_CACHE || join(dirname(reports), 'dsharp-nuget-cache'));
  const keys = ['PATH', 'HOME', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'XDG_RUNTIME_DIR', 'HTTP_PROXY', 'HTTPS_PROXY', 'NO_PROXY', 'http_proxy', 'https_proxy', 'no_proxy', 'SSL_CERT_FILE'];
  const env = Object.fromEntries(keys.filter(k => process.env[k] !== undefined).map(k => [k, process.env[k]]));
  Object.assign(env, { NUGET_PACKAGES: cache, DOTNET_CLI_TELEMETRY_OPTOUT: '1' });
  // Only approved project/manifests execute during host-side restore.
  run('dotnet', ['restore', join(trusted, 'DSharp.slnx'), '--locked-mode', '--packages', cache], { cwd: trusted, env });
  config = mkdtempSync(join(tmpdir(), 'dsharp-candidate-'));
  const stage = join(config, 'workspace'); mkdirSync(stage);
  const uid = process.getuid(), gid = process.getgid();
  writeFileSync(join(config, 'passwd'), `root:x:0:0:root:/root:/bin/bash\nverifier:x:${uid}:${gid}:verifier:/tmp/home:/bin/bash\n`);
  writeFileSync(join(config, 'group'), `root:x:0:\nverifier:x:${gid}:\n`);
  writeFileSync(join(config, 'nsswitch.conf'), 'passwd: files\ngroup: files\nhosts: files dns\n');
  for (const name of ['global.json', 'Directory.Build.props', 'NuGet.Config', 'DSharp.slnx']) copy(join(trusted, name), join(stage, name));
  for (const folder of ['src', 'tests']) for (const source of files(join(trusted, folder))) copy(source, join(stage, relative(trusted, source)));
  for (const source of files(join(stage, 'src')).filter(p => p.endsWith('.cs'))) unlinkSync(source);
  for (const folder of ['src', 'tests/features']) for (const source of files(join(candidate, folder)).filter(p => p.endsWith('.cs'))) copy(source, join(stage, relative(candidate, source)));
  stagePreludeResource(candidate, stage);
  const features = files(join(stage, 'tests/features')).some(p => p.endsWith('.cs'));
  // Operator-only local override; workflows always use the reviewed image pin.
  const image = process.env.AI_COMPILER_IMAGE || readFileSync(join(trusted, '.github/ai/compiler-image.txt'), 'utf8').trim();
  const script = `set -euo pipefail
cd /work
test "$(dotnet --version)" = 11.0.100
dotnet restore DSharp.slnx --locked-mode --packages /packages --source /packages -p:NuGetAudit=false
dotnet build DSharp.slnx --no-restore -c Release
echo success > build-status
dotnet test tests/DSharp.Compiler.Tests --no-build --no-restore -c Release --logger 'trx;LogFileName=acceptance.trx' --results-directory /work/results
if [[ "$1" = yes ]]; then
  dotnet test tests/DSharp.Compiler.Tests --no-restore -c Release -p:DSharpTestGroup=Features --logger 'trx;LogFileName=features.trx' --results-directory /work/results
fi
`;
  const args = ['run', '--rm', '--cidfile', join(config, 'cid'), '--network=none', '--cap-drop=ALL', '--security-opt=no-new-privileges',
    '--pids-limit=256', '--memory=2g', '--cpus=2', '--read-only', '--user', `${uid}:${gid}`, '--tmpfs', '/tmp:rw,nosuid,nodev,size=512m'];
  for (const [source, destination, readonly] of [[stage, '/work', false], [join(config, 'passwd'), '/etc/passwd', true], [join(config, 'group'), '/etc/group', true], [join(config, 'nsswitch.conf'), '/etc/nsswitch.conf', true], [cache, '/packages', true]]) args.push('--mount', `type=bind,src=${source},dst=${destination}${readonly ? ',readonly' : ''}`);
  for (const key of ['HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'http_proxy', 'https_proxy']) args.push('-e', `${key}=`);
  args.push(image, '/usr/bin/env', '-i', 'PATH=/usr/share/dotnet:/usr/bin:/bin', 'DOTNET_ROOT=/usr/share/dotnet', 'HOME=/tmp/home', 'DOTNET_CLI_HOME=/tmp/home', 'NUGET_PACKAGES=/packages', 'DOTNET_CLI_TELEMETRY_OPTOUT=1', 'DOTNET_PROCESSOR_COUNT=2', 'USER=verifier', 'LOGNAME=verifier', '/bin/bash', '-c', script, 'verify', features ? 'yes' : 'no');
  let completed;
  try { completed = spawnSync('docker', args, { stdio: 'inherit', timeout: 480000, killSignal: 'SIGKILL' }); }
  finally {
    const cid = join(config, 'cid');
    if (existsSync(cid)) spawnSync('docker', ['rm', '-f', readFileSync(cid, 'utf8').trim()], { stdio: 'ignore', timeout: 30000 });
  }
  const marker = join(stage, 'build-status');
  if (existsSync(marker) && !lstatSync(marker).isSymbolicLink() && readFileSync(marker, 'utf8').trim() === 'success') evidence.build = 'success';
  for (const name of ['acceptance', 'features']) {
    if (name === 'features' && !features) continue;
    const path = join(stage, 'results', `${name}.trx`);
    if (!existsSync(path) && completed.status !== 0) continue;
    assert(!lstatSync(join(stage, 'results')).isSymbolicLink(), 'Unsafe report directory');
    assert(!lstatSync(path).isSymbolicLink() && lstatSync(path).size <= 512000, 'Unsafe/oversized report');
    copy(path, join(reports, `${name}.trx`));
    evidence[name] = compilerResults(join(reports, `${name}.trx`));
    console.log(JSON.stringify(evidence[name]));
  }
  if (completed.error) throw completed.error;
  assert(completed.status === 0, 'Offline compiler build/test failed');
  assert(evidence.acceptance?.total >= 18, 'Trusted acceptance tests missing');
} finally {
  if (config) rmSync(config, { recursive: true, force: true });
  writeFileSync(join(reports, 'compiler.json'), JSON.stringify(evidence));
}
