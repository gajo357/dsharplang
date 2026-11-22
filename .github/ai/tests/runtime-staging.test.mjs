import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { stageRuntime } from '../verify-runtime.mjs';

function put(root, path, content) {
  const destination = join(root, path);
  mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, content);
  return destination;
}
function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-runtime-stage-test-'));
  const trusted = join(root, 'trusted'), candidate = join(root, 'candidate'), stage = join(root, 'stage');
  for (const dir of [trusted, candidate, stage]) mkdirSync(dir);
  for (const name of ['global.json', 'Directory.Build.props', 'NuGet.Config']) put(trusted, name, 'trusted manifest');
  put(trusted, 'src/DSharp.Runtime/DSharp.Runtime.csproj', 'trusted project');
  put(trusted, 'src/DSharp.Runtime/packages.lock.json', 'trusted lock');
  put(trusted, 'src/DSharp.Runtime/Runtime.cs', 'old runtime');
  put(trusted, 'tests/DSharp.Runtime.Tests/DSharp.Runtime.Tests.csproj', 'trusted test project');
  put(trusted, 'tests/DSharp.Runtime.Tests/Baseline.cs', 'trusted baseline');
  put(trusted, 'tests/runtime-features/Existing.cs', 'immutable test');
  put(candidate, 'src/DSharp.Runtime/Runtime.cs', 'candidate runtime');
  put(candidate, 'src/DSharp.Runtime/DSharp.Runtime.csproj', 'hostile project');
  put(candidate, 'tests/DSharp.Runtime.Tests/Baseline.cs', 'hostile baseline');
  put(candidate, 'tests/runtime-features/Existing.cs', 'immutable test');
  put(candidate, 'tests/runtime-features/New.cs', 'new feature');
  return { root, trusted, candidate, stage };
}

test('runtime staging keeps trusted manifests and baseline while adding only permitted candidate files', () => {
  const f = fixture();
  try {
    stageRuntime(f.trusted, f.candidate, f.stage);
    for (const [path, value] of [
      ['src/DSharp.Runtime/DSharp.Runtime.csproj', 'trusted project'],
      ['src/DSharp.Runtime/packages.lock.json', 'trusted lock'],
      ['src/DSharp.Runtime/Runtime.cs', 'candidate runtime'],
      ['tests/DSharp.Runtime.Tests/Baseline.cs', 'trusted baseline'],
      ['tests/runtime-features/Existing.cs', 'immutable test'],
      ['tests/runtime-features/New.cs', 'new feature']
    ]) assert.equal(readFileSync(join(f.stage, path), 'utf8'), value);
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});

test('runtime staging rejects modification and deletion of trusted features', () => {
  for (const action of ['change', 'delete']) {
    const f = fixture();
    try {
      const existing = join(f.candidate, 'tests/runtime-features/Existing.cs');
      if (action === 'change') writeFileSync(existing, 'weakened'); else rmSync(existing);
      assert.throws(() => stageRuntime(f.trusted, f.candidate, f.stage), /trusted runtime feature test/);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('runtime staging rejects symlinked source and feature paths', () => {
  for (const path of ['src/DSharp.Runtime/Runtime.cs', 'tests/runtime-features/Existing.cs', 'src/DSharp.Runtime']) {
    const f = fixture();
    try {
      const target = join(f.candidate, path);
      rmSync(target, { recursive: true, force: true });
      symlinkSync(join(f.trusted, path), target);
      assert.throws(() => stageRuntime(f.trusted, f.candidate, f.stage), /[Ss]ymlink|Unsafe/);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('each runtime source modification, addition or deletion requires a new feature test', () => {
  for (const action of ['modify', 'add', 'delete']) {
    const f = fixture();
    try {
      // Keep a common source so the deletion case is not merely an empty project.
      put(f.trusted, 'src/DSharp.Runtime/Shared.cs', 'shared source');
      put(f.candidate, 'src/DSharp.Runtime/Shared.cs', 'shared source');
      put(f.candidate, 'src/DSharp.Runtime/Runtime.cs', 'old runtime');
      rmSync(join(f.candidate, 'tests/runtime-features/New.cs'));
      if (action === 'modify') put(f.candidate, 'src/DSharp.Runtime/Runtime.cs', 'modified runtime');
      if (action === 'add') put(f.candidate, 'src/DSharp.Runtime/Nested/Added.cs', 'new runtime source');
      if (action === 'delete') rmSync(join(f.candidate, 'src/DSharp.Runtime/Runtime.cs'));
      assert.throws(() => stageRuntime(f.trusted, f.candidate, f.stage), /new runtime feature test/i, action);
      put(f.candidate, 'tests/runtime-features/New.cs', 'new feature');
      assert.doesNotThrow(() => stageRuntime(f.trusted, f.candidate, f.stage), action);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('unchanged runtime source may use the existing feature tests', () => {
  const f = fixture();
  try {
    put(f.candidate, 'src/DSharp.Runtime/Runtime.cs', 'old runtime');
    rmSync(join(f.candidate, 'tests/runtime-features/New.cs'));
    stageRuntime(f.trusted, f.candidate, f.stage);
    assert.equal(readFileSync(join(f.stage, 'tests/runtime-features/Existing.cs'), 'utf8'), 'immutable test');
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});
