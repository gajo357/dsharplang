import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { assertNewFeatureDiscovered, stageSqlite } from '../verify-sqlite.mjs';

function put(root, path, content) {
  const destination = join(root, path);
  mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, content);
  return destination;
}
function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-sqlite-stage-test-'));
  const trusted = join(root, 'trusted'), candidate = join(root, 'candidate'), stage = join(root, 'stage');
  for (const dir of [trusted, candidate, stage]) mkdirSync(dir);
  for (const name of ['global.json', 'Directory.Build.props', 'NuGet.Config']) put(trusted, name, `trusted ${name}`);
  put(trusted, 'src/DSharp.Runtime/DSharp.Runtime.csproj', 'trusted core project');
  put(trusted, 'src/DSharp.Runtime/Core.cs', 'trusted core');
  put(trusted, 'src/DSharp.Runtime.Sqlite/DSharp.Runtime.Sqlite.csproj', 'trusted SQLite project');
  put(trusted, 'src/DSharp.Runtime.Sqlite/packages.lock.json', 'trusted SQLite lock');
  put(trusted, 'src/DSharp.Runtime.Sqlite/Source.cs', 'old SQLite source');
  put(trusted, 'tests/DSharp.Runtime.Sqlite.Tests/DSharp.Runtime.Sqlite.Tests.csproj', 'trusted test project');
  put(trusted, 'tests/DSharp.Runtime.Sqlite.Tests/packages.lock.json', 'trusted test lock');
  put(trusted, 'tests/DSharp.Runtime.Sqlite.Tests/Baseline.cs', 'trusted baseline');
  put(trusted, 'tests/sqlite-features/Existing.cs', 'immutable feature');
  put(candidate, 'src/DSharp.Runtime/Core.cs', 'hostile core');
  put(candidate, 'src/DSharp.Runtime/DSharp.Runtime.csproj', 'hostile core project');
  put(candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'candidate SQLite source');
  put(candidate, 'src/DSharp.Runtime.Sqlite/DSharp.Runtime.Sqlite.csproj', 'hostile SQLite project');
  put(candidate, 'src/DSharp.Runtime.Sqlite/packages.lock.json', 'hostile SQLite lock');
  put(candidate, 'tests/DSharp.Runtime.Sqlite.Tests/Baseline.cs', 'hostile baseline');
  put(candidate, 'tests/DSharp.Runtime.Sqlite.Tests/DSharp.Runtime.Sqlite.Tests.csproj', 'hostile test project');
  put(candidate, 'tests/sqlite-features/Existing.cs', 'immutable feature');
  put(candidate, 'tests/sqlite-features/New.cs', 'candidate feature');
  return { root, trusted, candidate, stage };
}

test('SQLite staging overlays candidate core and SQLite source while retaining trusted manifests', () => {
  const f = fixture();
  try {
    const selected = stageSqlite(f.trusted, f.candidate, f.stage);
    assert.equal(selected.sourceChanged, true);
    assert.equal(selected.existingFeatureCount, 1);
    assert.deepEqual(selected.addedFeatures, ['tests/sqlite-features/New.cs']);
    for (const [path, expected] of [
      ['global.json', 'trusted global.json'],
      ['src/DSharp.Runtime/DSharp.Runtime.csproj', 'trusted core project'],
      ['src/DSharp.Runtime/Core.cs', 'hostile core'],
      ['src/DSharp.Runtime.Sqlite/DSharp.Runtime.Sqlite.csproj', 'trusted SQLite project'],
      ['src/DSharp.Runtime.Sqlite/packages.lock.json', 'trusted SQLite lock'],
      ['src/DSharp.Runtime.Sqlite/Source.cs', 'candidate SQLite source'],
      ['tests/DSharp.Runtime.Sqlite.Tests/DSharp.Runtime.Sqlite.Tests.csproj', 'trusted test project'],
      ['tests/DSharp.Runtime.Sqlite.Tests/packages.lock.json', 'trusted test lock'],
      ['tests/DSharp.Runtime.Sqlite.Tests/Baseline.cs', 'trusted baseline'],
      ['tests/sqlite-features/Existing.cs', 'immutable feature'],
      ['tests/sqlite-features/New.cs', 'candidate feature']
    ]) assert.equal(readFileSync(join(f.stage, path), 'utf8'), expected, path);
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});

test('SQLite staging refuses altered or deleted trusted feature tests', () => {
  for (const action of ['change', 'delete']) {
    const f = fixture();
    try {
      const path = join(f.candidate, 'tests/sqlite-features/Existing.cs');
      if (action === 'change') writeFileSync(path, 'weakened'); else rmSync(path);
      assert.throws(() => stageSqlite(f.trusted, f.candidate, f.stage), /trusted SQLite feature test/);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('SQLite staging rejects symlinks in files, roots and ancestor components', () => {
  for (const path of ['src/DSharp.Runtime/Core.cs', 'src/DSharp.Runtime', 'src/DSharp.Runtime.Sqlite/Source.cs', 'tests/sqlite-features/Existing.cs', 'src/DSharp.Runtime.Sqlite', 'src']) {
    const f = fixture();
    try {
      const target = join(f.candidate, path);
      rmSync(target, { recursive: true, force: true });
      symlinkSync(join(f.trusted, path), target);
      assert.throws(() => stageSqlite(f.trusted, f.candidate, f.stage), /[Ss]ymlink|Unsafe/);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
  const f = fixture();
  try {
    const alias = join(f.root, 'alias');
    symlinkSync(f.candidate, alias);
    assert.throws(() => stageSqlite(f.trusted, alias, f.stage), /[Ss]ymlink|Unsafe/);
    const manifest = join(f.trusted, 'global.json');
    rmSync(manifest);
    symlinkSync(join(f.trusted, 'NuGet.Config'), manifest);
    assert.throws(() => stageSqlite(f.trusted, f.candidate, f.stage), /[Ss]ymlink|Unsafe/);
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});

test('source modifications, additions and deletions require newly added features', () => {
  for (const action of ['modify', 'add', 'delete']) {
    const f = fixture();
    try {
      put(f.trusted, 'src/DSharp.Runtime.Sqlite/Shared.cs', 'shared');
      put(f.candidate, 'src/DSharp.Runtime.Sqlite/Shared.cs', 'shared');
      put(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'old SQLite source');
      rmSync(join(f.candidate, 'tests/sqlite-features/New.cs'));
      if (action === 'modify') put(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'changed');
      if (action === 'add') put(f.candidate, 'src/DSharp.Runtime.Sqlite/Nested/Added.cs', 'added');
      if (action === 'delete') rmSync(join(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs'));
      assert.throws(() => stageSqlite(f.trusted, f.candidate, f.stage), /new SQLite feature test/i, action);
      put(f.candidate, 'tests/sqlite-features/New.cs', 'new test');
      assert.doesNotThrow(() => stageSqlite(f.trusted, f.candidate, f.stage), action);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('unchanged source may reuse trusted feature tests', () => {
  const f = fixture();
  try {
    put(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'old SQLite source');
    rmSync(join(f.candidate, 'tests/sqlite-features/New.cs'));
    const selection = stageSqlite(f.trusted, f.candidate, f.stage);
    assert.equal(selection.sourceChanged, false);
    assert.equal(selection.integrationSourceChanged, false);
    assert.deepEqual(selection.addedFeatures, []);
    assert.equal(readFileSync(join(f.stage, 'tests/sqlite-features/Existing.cs'), 'utf8'), 'immutable feature');
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});

test('a changed, added or deleted core coordinator requires a new SQLite feature', () => {
  for (const action of ['modify', 'add', 'delete']) {
    const f = fixture();
    try {
      put(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'old SQLite source');
      rmSync(join(f.candidate, 'tests/sqlite-features/New.cs'));
      const path = 'src/DSharp.Runtime/ActorSchemaMigrationCoordinator.cs';
      if (action !== 'add') put(f.trusted, path, 'trusted coordinator');
      if (action !== 'delete') put(f.candidate, path, action === 'modify' ? 'changed coordinator' : 'new coordinator');
      assert.throws(() => stageSqlite(f.trusted, f.candidate, f.stage), /new SQLite feature test/i, action);
      put(f.candidate, 'tests/sqlite-features/New.cs', 'new SQLite test');
      const selection = stageSqlite(f.trusted, f.candidate, f.stage);
      assert.equal(selection.integrationSourceChanged, true, action);
      assert.equal(selection.sqliteSourceChanged, false, action);
      assert.equal(selection.sourceChanged, true, action);
    } finally { rmSync(f.root, { recursive: true, force: true }); }
  }
});

test('unrelated core source changes do not require new SQLite features', () => {
  const f = fixture();
  try {
    put(f.candidate, 'src/DSharp.Runtime.Sqlite/Source.cs', 'old SQLite source');
    rmSync(join(f.candidate, 'tests/sqlite-features/New.cs'));
    const selection = stageSqlite(f.trusted, f.candidate, f.stage);
    assert.equal(selection.sourceChanged, false);
    assert.equal(selection.integrationSourceChanged, false);
    assert.equal(readFileSync(join(f.stage, 'src/DSharp.Runtime/Core.cs'), 'utf8'), 'hostile core');
    assert.equal(readFileSync(join(f.stage, 'src/DSharp.Runtime/DSharp.Runtime.csproj'), 'utf8'), 'trusted core project');
  } finally { rmSync(f.root, { recursive: true, force: true }); }
});

test('new feature count must increase on changed source', () => {
  assert.doesNotThrow(() => assertNewFeatureDiscovered(1, 2));
  for (const [prior, next] of [[0, 0], [1, 1], [2, 1], [-1, 1], [0.5, 1], [0, Number.NaN]])
    assert.throws(() => assertNewFeatureDiscovered(prior, next));
});
