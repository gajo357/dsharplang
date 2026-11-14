import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { stagePreludeResource } from '../prelude-resource.mjs';

const repository = fileURLToPath(new URL('../../..', import.meta.url));
const relativePrelude = 'src/DSharp.Compiler/Prelude/Result.ds';
const resourceName = 'DSharp.Compiler.Prelude.Result.ds';

function put(root, path, contents) {
  const destination = join(root, path);
  mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, contents);
  return destination;
}

test('prelude staging overlays only the exact candidate source bytes', () => {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-prelude-stage-'));
  try {
    const candidate = join(root, 'candidate');
    const stage = join(root, 'stage');
    mkdirSync(candidate); mkdirSync(stage);
    const replacement = Buffer.from([0, 13, 10, 0xc3, 0xa9, 255]);
    put(candidate, relativePrelude, replacement);
    put(candidate, 'src/DSharp.Compiler/Prelude/Other.ds', 'untrusted other');
    put(stage, relativePrelude, 'old trusted bytes');
    put(stage, 'src/DSharp.Compiler/Prelude/Other.ds', 'trusted other');
    put(stage, 'tests/features/Existing.cs', 'trusted test');
    assert.throws(() => stagePreludeResource(candidate, candidate));
    stagePreludeResource(candidate, stage);
    assert.deepEqual(readFileSync(join(stage, relativePrelude)), replacement);
    assert.equal(readFileSync(join(stage, 'src/DSharp.Compiler/Prelude/Other.ds'), 'utf8'), 'trusted other');
    assert.equal(readFileSync(join(stage, 'tests/features/Existing.cs'), 'utf8'), 'trusted test');
    // A missing candidate must not leave the trusted bytes silently in use.
    rmSync(join(candidate, relativePrelude));
    assert.throws(() => stagePreludeResource(candidate, stage));
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('prelude staging rejects source and destination symlinks and unsafe paths', () => {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-prelude-links-'));
  try {
    const candidate = join(root, 'candidate');
    const stage = join(root, 'stage');
    const outside = join(root, 'outside');
    mkdirSync(candidate); mkdirSync(stage); mkdirSync(outside);
    const source = put(candidate, relativePrelude, 'safe');
    const outsideFile = put(outside, 'Result.ds', 'outside unchanged');
    rmSync(source);
    symlinkSync(outsideFile, source);
    assert.throws(() => stagePreludeResource(candidate, stage));
    rmSync(source);
    put(candidate, relativePrelude, 'safe');
    const sourceParent = dirname(source);
    rmSync(sourceParent, { recursive: true });
    symlinkSync(outside, sourceParent, 'dir');
    assert.throws(() => stagePreludeResource(candidate, stage));
    rmSync(sourceParent);
    put(candidate, relativePrelude, 'safe');
    const destination = put(stage, relativePrelude, 'stage original');
    rmSync(destination);
    symlinkSync(outsideFile, destination);
    assert.throws(() => stagePreludeResource(candidate, stage));
    assert.equal(readFileSync(outsideFile, 'utf8'), 'outside unchanged');
    rmSync(destination);
    rmSync(dirname(destination), { recursive: true });
    symlinkSync(outside, dirname(destination), 'dir');
    assert.throws(() => stagePreludeResource(candidate, stage));
    assert.equal(readFileSync(outsideFile, 'utf8'), 'outside unchanged');
    const candidateAlias = join(root, 'candidate-alias');
    symlinkSync(candidate, candidateAlias, 'dir');
    assert.throws(() => stagePreludeResource(candidateAlias, stage));
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('packaged compiler resource is extracted and executed as D# outside the checkout', { timeout: 180000 }, () => {
  const root = mkdtempSync(join(tmpdir(), 'dsharp-prelude-package-'));
  const run = (command, args, cwd) => {
    const result = spawnSync(command, args, { cwd, encoding: 'utf8', timeout: 120000 });
    assert.equal(result.status, 0, `${command} ${args.join(' ')}\n${result.stdout}\n${result.stderr}\n${result.error ?? ''}`);
    return result.stdout.trim();
  };
  try {
    // Build the actual shipped CLI and its compiler reference, not a duplicated
    // in-test source. All later commands execute from a directory outside git.
    run('dotnet', ['build', 'src/DSharp.Cli/DSharp.Cli.csproj', '--configuration', 'Release', '--nologo'], repository);
    const cli = join(repository, 'src/DSharp.Cli/bin/Release/net11.0/dsharp.dll');
    const compiler = join(repository, 'src/DSharp.Cli/bin/Release/net11.0/DSharp.Compiler.dll');
    assert.ok(existsSync(cli)); assert.ok(existsSync(compiler));
    put(root, 'global.json', '{"sdk":{"version":"11.0.100","rollForward":"disable"}}\n');
    put(root, 'probe/Probe.csproj', '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>\n');
    put(root, 'probe/Program.cs', `using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
var assembly = Assembly.LoadFrom(args[0]);
if (args[1] == "resource") {
    using var stream = assembly.GetManifestResourceStream(args[2]) ?? throw new Exception("Missing embedded prelude");
    using var copy = new MemoryStream();
    stream.CopyTo(copy);
    var bytes = copy.ToArray();
    File.WriteAllBytes(args[3], bytes);
    Console.WriteLine(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
} else {
    var value = assembly.GetTypes().Single(type => type.Name == "Demo").GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);
    Console.WriteLine(value);
}
`);
    run('dotnet', ['build', 'probe/Probe.csproj', '--configuration', 'Release', '--nologo'], root);
    const probe = join(root, 'probe/bin/Release/net11.0/Probe.dll');
    const sourcePath = join(repository, relativePrelude);
    const extractedPath = join(root, 'embedded-result.ds');
    const expectedHash = createHash('sha256').update(readFileSync(sourcePath)).digest('hex');
    assert.equal(run('dotnet', [probe, compiler, 'resource', resourceName, extractedPath], root), expectedHash);
    assert.deepEqual(readFileSync(extractedPath), readFileSync(sourcePath));

    // This is a consumer only: the helper implementation is always read from the
    // embedded resource, then compiled with the normal multi-file D# frontend.
    const consumer = put(root, 'consumer.ds', `module Demo
import Prelude
fn next(value: int): Result<int, string> { Success(value + 1) }
fn convert(error: string): int { 99 }
fn observe(value: int): Unit { () }
fn zero(): int { 0 }
fn badMap(value: int): int { 1 / zero() }
fn badNext(value: int): Result<int, string> { Success(1 / zero()) }
fn badObserve(value: int): Unit { let unused = 1 / zero()
    () }
fn badError(error: string): int { 1 / zero() }
fn badErrorObserve(error: string): Unit { let unused = 1 / zero()
    () }
fn answer(): int {
    let result: Result<int, string> = Success(40)
    let chained = Prelude.andThen(next, result)
    let mapped = Prelude.map(value => value + 1, chained)
    let success = Prelude.tap(observe, mapped)
    let failure: Result<int, string> = Error("failed")
    let skipNext = Prelude.andThen(badNext, failure)
    let skipMap = Prelude.map(badMap, failure)
    let skipTap = Prelude.tap(badObserve, failure)
    let skipErrorMap = Prelude.mapError(badError, result)
    let skipErrorTap = Prelude.tapError(badErrorObserve, result)
    let nested = Prelude.map(next, result)
    let nestedPreserved = match nested {
        Success(Success(value)) => value == 41
        Success(Error(error)) => false
        Error(error) => false
    }
    let optional: Result<Option<string?>, string> = Success(Some(null))
    let mappedOptional = Prelude.map(value => value, optional)
    let optionalPreserved = match mappedOptional {
        Success(Some(null)) => true
        Success(Some(value)) => false
        Success(None) => false
        Error(error) => false
    }
    let missing: Result<Option<string?>, string> = Success(None)
    let mappedMissing = Prelude.map(value => value, missing)
    let nonePreserved = match mappedMissing {
        Success(None) => true
        Success(Some(null)) => false
        Success(Some(value)) => false
        Error(error) => false
    }
    let errorsSkipped = (match skipNext { Success(value) => false
        Error(error) => error == "failed" }) &&
        (match skipMap { Success(value) => false
        Error(error) => error == "failed" }) &&
        (match skipTap { Success(value) => false
        Error(error) => error == "failed" })
    let successPreserved = (match skipErrorMap { Success(value) => value == 40
        Error(error) => false }) &&
        (match skipErrorTap { Success(value) => value == 40
        Error(error) => false })
    let converted = Prelude.mapError(convert, failure)
    let explicitConverted = Prelude.mapError<int, int, string>(convert, failure)
    let observed = Prelude.tapError(observe, converted)
    let code = match observed {
        Success(value) => 0
        Error(error) => error
    }
    let explicitCode = match explicitConverted {
        Success(value) => 0
        Error(error) => error
    }
    match success {
        Success(value) => if code == 99 && explicitCode == 99 && errorsSkipped && successPreserved && nestedPreserved && optionalPreserved && nonePreserved { value } else { 0 }
        Error(error) => 0
    }
}
`);
    run('dotnet', [cli, 'check', extractedPath, consumer], root);
    const output = join(root, 'generated');
    run('dotnet', [cli, 'compile', extractedPath, consumer, output], root);
    assert.equal(run('dotnet', [probe, join(output, 'bin/Debug/net11.0/Generated.dll'), 'execute'], root), '42');
  } finally { rmSync(root, { recursive: true, force: true }); }
});
