// Trusted orchestration: candidate runtime code runs only in an offline container.
import assert from 'node:assert/strict';
import { copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { compilerResults, files } from '../../scripts/validation.mjs';

const trusted = fileURLToPath(new URL('../..', import.meta.url));
const runtimeSource = 'src/DSharp.Runtime';
const baselineTests = 'tests/DSharp.Runtime.Tests';
const featureTests = 'tests/runtime-features';
function copy(source, destination) {
  mkdirSync(dirname(destination), { recursive: true });
  copyFileSync(source, destination);
}

// Verify every existing path component rather than following an ancestor symlink.
function checkedDirectory(root, path, required) {
  let current = root;
  assert(lstatSync(current).isDirectory() && !lstatSync(current).isSymbolicLink(), 'Unsafe root');
  for (const component of path.split('/')) {
    current = join(current, component);
    if (!existsSync(current)) {
      // existsSync is false for dangling links; lstat must still reject those.
      try { lstatSync(current); throw new Error(`Unsafe path: ${current}`); }
      catch (error) { if (error.code !== 'ENOENT') throw error; }
      assert(!required, `Missing directory: ${current}`);
      return false;
    }
    const stat = lstatSync(current);
    assert(stat.isDirectory() && !stat.isSymbolicLink(), `Unsafe directory: ${current}`);
  }
  return true;
}

// Keep reviewed manifests and baseline tests from trusted; allow only source and
// new feature .cs files from the candidate. Existing feature tests are immutable.
export function stageRuntime(trustedRoot, candidateRoot, stage) {
  for (const name of ['global.json', 'Directory.Build.props', 'NuGet.Config']) copy(join(trustedRoot, name), join(stage, name));
  for (const path of [runtimeSource, baselineTests]) {
    assert(checkedDirectory(trustedRoot, path, true));
    for (const source of files(join(trustedRoot, path))) copy(source, join(stage, relative(trustedRoot, source)));
  }
  assert(checkedDirectory(candidateRoot, runtimeSource, true));
  const trustedSources = files(join(trustedRoot, runtimeSource)).filter(p => p.endsWith('.cs'));
  for (const source of files(join(stage, runtimeSource)).filter(p => p.endsWith('.cs'))) unlinkSync(source);
  const candidateSources = files(join(candidateRoot, runtimeSource)).filter(p => p.endsWith('.cs'));
  assert(candidateSources.length, 'No candidate runtime source');
  const trustedSourcePaths = new Set(trustedSources.map(p => relative(trustedRoot, p)));
  const candidateSourcePaths = new Set(candidateSources.map(p => relative(candidateRoot, p)));
  const sourceChanged = trustedSources.some(p => {
    const path = relative(trustedRoot, p);
    return !candidateSourcePaths.has(path) || !readFileSync(p).equals(readFileSync(join(candidateRoot, path)));
  }) || candidateSources.some(p => !trustedSourcePaths.has(relative(candidateRoot, p)));
  for (const source of candidateSources) copy(source, join(stage, relative(candidateRoot, source)));

  const hasTrustedFeatures = checkedDirectory(trustedRoot, featureTests, false);
  const hasCandidateFeatures = checkedDirectory(candidateRoot, featureTests, false);
  const existing = hasTrustedFeatures ? files(join(trustedRoot, featureTests)).filter(p => p.endsWith('.cs')) : [];
  const candidate = hasCandidateFeatures ? files(join(candidateRoot, featureTests)).filter(p => p.endsWith('.cs')) : [];
  assert(candidate.length, 'No runtime feature test source');
  const candidatePaths = new Set(candidate.map(p => relative(candidateRoot, p)));
  for (const source of existing) {
    const path = relative(trustedRoot, source);
    assert(candidatePaths.has(path), `Deleted trusted runtime feature test: ${path}`);
    assert(readFileSync(source).equals(readFileSync(join(candidateRoot, path))), `Modified trusted runtime feature test: ${path}`);
    copy(source, join(stage, path));
  }
  const existingPaths = new Set(existing.map(p => relative(trustedRoot, p)));
  const addedFeatures = candidate.map(p => relative(candidateRoot, p)).filter(path => !existingPaths.has(path));
  assert(!sourceChanged || addedFeatures.length > 0, 'Runtime source changes require a new runtime feature test');
  for (const source of candidate) {
    const path = relative(candidateRoot, source);
    if (!existingPaths.has(path)) copy(source, join(stage, path));
  }
  return { sourceChanged, existingFeatureCount: existing.length, addedFeatures };
}

// When source changes, new feature files must add discovered tests, not merely
// exist. The prior count comes from a separate run of only trusted feature files.
export function assertNewFeatureDiscovered(prior, aggregate) {
  assert(Number.isSafeInteger(prior) && prior >= 0 && Number.isSafeInteger(aggregate) && aggregate > prior,
    'Runtime source changes require at least one newly discovered passing feature test');
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  assert(result.status === 0, `${command} failed (${result.status ?? result.signal})`);
}

function main() {
  const candidate = resolve(process.argv[2]);
  const reports = resolve(process.argv[3]);
  mkdirSync(reports, { recursive: true });
  assert(lstatSync(reports).isDirectory() && !lstatSync(reports).isSymbolicLink(), 'Unsafe reports directory');
  for (const name of ['runtime-baseline.trx', 'runtime-prior-features.trx', 'runtime-features.trx', 'runtime.json']) {
    const path = join(reports, name);
    try { unlinkSync(path); } catch (error) { if (error.code !== 'ENOENT') throw error; }
  }
  const evidence = { attempted: true, build: 'failure' };
  let config;
  try {
    const cache = resolve(process.env.AI_NUGET_CACHE || join(dirname(reports), 'dsharp-nuget-cache'));
    const keys = ['PATH', 'HOME', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'XDG_RUNTIME_DIR', 'HTTP_PROXY', 'HTTPS_PROXY', 'NO_PROXY', 'http_proxy', 'https_proxy', 'no_proxy', 'SSL_CERT_FILE'];
    const env = Object.fromEntries(keys.filter(k => process.env[k] !== undefined).map(k => [k, process.env[k]]));
    Object.assign(env, { NUGET_PACKAGES: cache, DOTNET_CLI_TELEMETRY_OPTOUT: '1' });
    run('dotnet', ['restore', join(trusted, baselineTests, 'DSharp.Runtime.Tests.csproj'), '--locked-mode', '--packages', cache], { cwd: trusted, env });
    config = mkdtempSync(join(tmpdir(), 'dsharp-runtime-candidate-'));
    const baselineStage = join(config, 'baseline'); mkdirSync(baselineStage);
    const selection = stageRuntime(trusted, candidate, baselineStage);
    const uid = process.getuid(), gid = process.getgid();
    writeFileSync(join(config, 'passwd'), `root:x:0:0:root:/root:/bin/bash\nverifier:x:${uid}:${gid}:verifier:/tmp/home:/bin/bash\n`);
    writeFileSync(join(config, 'group'), `root:x:0:\nverifier:x:${gid}:\n`);
    writeFileSync(join(config, 'nsswitch.conf'), 'passwd: files\ngroup: files\nhosts: files dns\n');
    const image = process.env.AI_COMPILER_IMAGE || readFileSync(join(trusted, '.github/ai/compiler-image.txt'), 'utf8').trim();
    const baselineScript = `set -euo pipefail
cd /w
test "$(dotnet --version)" = 11.0.100
dotnet restore tests/DSharp.Runtime.Tests/DSharp.Runtime.Tests.csproj --locked-mode --packages /packages --source /packages -p:NuGetAudit=false
dotnet build tests/DSharp.Runtime.Tests/DSharp.Runtime.Tests.csproj --no-restore -c Release
echo success > build-status
dotnet test tests/DSharp.Runtime.Tests --no-build --no-restore -c Release --logger 'trx;LogFileName=runtime-baseline.trx' --results-directory /w/results
`;
    const featureScript = `set -euo pipefail
cd /w
test "$(dotnet --version)" = 11.0.100
dotnet restore tests/DSharp.Runtime.Tests/DSharp.Runtime.Tests.csproj --locked-mode --packages /packages --source /packages -p:NuGetAudit=false
dotnet test tests/DSharp.Runtime.Tests --no-restore -c Release -p:DSharpTestGroup=RuntimeFeatures --logger 'trx;LogFileName=runtime-features.trx' --results-directory /w/results
`;
    function container(stage, script, name) {
      const cid = join(config, `${name}-cid`);
      const args = ['run', '--rm', '--cidfile', cid, '--network=none', '--cap-drop=ALL', '--security-opt=no-new-privileges', '--pids-limit=256', '--memory=2g', '--cpus=2', '--read-only', '--user', `${uid}:${gid}`, '--tmpfs', '/tmp:rw,nosuid,nodev,size=512m'];
      for (const [source, destination, readonly] of [[stage, '/w', false], [join(config, 'passwd'), '/etc/passwd', true], [join(config, 'group'), '/etc/group', true], [join(config, 'nsswitch.conf'), '/etc/nsswitch.conf', true], [cache, '/packages', true]]) args.push('--mount', `type=bind,src=${source},dst=${destination}${readonly ? ',readonly' : ''}`);
      for (const key of ['HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'http_proxy', 'https_proxy']) args.push('-e', `${key}=`);
      args.push(image, '/usr/bin/env', '-i', 'PATH=/usr/share/dotnet:/usr/bin:/bin', 'DOTNET_ROOT=/usr/share/dotnet', 'HOME=/tmp/home', 'DOTNET_CLI_HOME=/tmp/home', 'NUGET_PACKAGES=/packages', 'DOTNET_CLI_TELEMETRY_OPTOUT=1', 'DOTNET_PROCESSOR_COUNT=2', 'USER=verifier', 'LOGNAME=verifier', '/bin/bash', '-c', script);
      try { return spawnSync('docker', args, { stdio: 'inherit', timeout: 480000, killSignal: 'SIGKILL' }); }
      finally { if (existsSync(cid)) spawnSync('docker', ['rm', '-f', readFileSync(cid, 'utf8').trim()], { stdio: 'ignore', timeout: 30000 }); }
    }
    function collect(stage, name, key) {
      const path = join(stage, 'results', `${name}.trx`);
      assert(!lstatSync(join(stage, 'results')).isSymbolicLink(), 'Unsafe report directory');
      assert(!lstatSync(path).isSymbolicLink() && lstatSync(path).isFile() && lstatSync(path).size <= 512000, 'Unsafe/oversized report');
      copy(path, join(reports, `${name}.trx`));
      evidence[key] = compilerResults(join(reports, `${name}.trx`));
      evidence[key].suite = name;
      console.log(JSON.stringify(evidence[key]));
    }
    const baseline = container(baselineStage, baselineScript, 'baseline');
    const marker = join(baselineStage, 'build-status');
    if (existsSync(marker) && !lstatSync(marker).isSymbolicLink() && readFileSync(marker, 'utf8').trim() === 'success') evidence.build = 'success';
    if (baseline.error) throw baseline.error;
    collect(baselineStage, 'runtime-baseline', 'baseline');
    assert(baseline.status === 0 && evidence.build === 'success' && evidence.baseline.total >= 2, 'Offline runtime baseline failed or incomplete');

    let priorCount = 0;
    if (selection.sourceChanged && selection.existingFeatureCount > 0) {
      const priorStage = join(config, 'prior-features'); mkdirSync(priorStage);
      const priorSelection = stageRuntime(trusted, candidate, priorStage);
      for (const path of priorSelection.addedFeatures) unlinkSync(join(priorStage, path));
      const priorScript = featureScript.replace('runtime-features.trx', 'runtime-prior-features.trx');
      const prior = container(priorStage, priorScript, 'prior-features');
      if (prior.error) throw prior.error;
      collect(priorStage, 'runtime-prior-features', 'priorFeatures');
      assert(prior.status === 0, 'Offline prior runtime feature tests failed');
      priorCount = evidence.priorFeatures.total;
    }

    // Capture and validate trusted baseline before candidate feature code runs.
    // Restage afresh so baseline execution cannot mutate the feature inputs.
    const featureStage = join(config, 'features'); mkdirSync(featureStage);
    stageRuntime(trusted, candidate, featureStage);
    const feature = container(featureStage, featureScript, 'features');
    if (feature.error) throw feature.error;
    // Preserve a safely copied failing TRX before strict parsing rejects it.
    collect(featureStage, 'runtime-features', 'features');
    assert(feature.status === 0, 'Offline runtime feature test failed');
    assert(evidence.baseline?.total >= 2 && evidence.features?.total > 0, 'Runtime baseline and feature tests required');
    if (selection.sourceChanged) assertNewFeatureDiscovered(priorCount, evidence.features.total);
  } finally {
    if (config) rmSync(config, { recursive: true, force: true });
    writeFileSync(join(reports, 'runtime.json'), JSON.stringify(evidence));
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
