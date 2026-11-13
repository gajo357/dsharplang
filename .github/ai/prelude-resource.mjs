// Stage only the reviewed compiler-owned D# prelude. Do not follow links in
// either tree: candidate content is untrusted and the stage must remain local.
import assert from 'node:assert/strict';
import { closeSync, constants, lstatSync, mkdirSync, openSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';

const components = ['src', 'DSharp.Compiler', 'Prelude', 'Result.ds'];

function checkedRoot(path) {
  const root = resolve(path);
  const stat = lstatSync(root);
  assert(!stat.isSymbolicLink() && stat.isDirectory(), `Unsafe staging root: ${root}`);
  return realpathSync(root);
}

function inside(root, path) {
  const location = relative(root, path);
  return location !== '..' && !location.startsWith(`..${sep}`) && !isAbsolute(location);
}

function checkedPath(root, createDirectories) {
  let path = root;
  for (let i = 0; i < components.length; i++) {
    path = join(path, components[i]);
    assert(inside(root, path), `Unsafe prelude path: ${path}`);
    const last = i === components.length - 1;
    let stat = lstatSync(path, { throwIfNoEntry: false });
    if (!stat && createDirectories) {
      if (last) return path;
      mkdirSync(path);
      stat = lstatSync(path);
    }
    assert(stat, `Missing prelude path: ${path}`);
    assert(!stat.isSymbolicLink(), `Symlink prelude path: ${path}`);
    assert(last ? stat.isFile() : stat.isDirectory(), `Invalid prelude path: ${path}`);
    assert(inside(root, realpathSync(path)), `Prelude escapes root: ${path}`);
  }
  return path;
}

export function stagePreludeResource(candidateRoot, stageRoot) {
  const candidate = checkedRoot(candidateRoot);
  const stage = checkedRoot(stageRoot);
  assert(candidate !== stage, 'Candidate and stage must be separate directories');
  const source = checkedPath(candidate, false);
  const destination = checkedPath(stage, true);
  const sourceFd = openSync(source, constants.O_RDONLY | constants.O_NOFOLLOW);
  let destinationFd;
  try {
    destinationFd = openSync(destination, constants.O_WRONLY | constants.O_CREAT | constants.O_TRUNC | constants.O_NOFOLLOW, 0o644);
    writeFileSync(destinationFd, readFileSync(sourceFd));
  } finally {
    if (destinationFd !== undefined) closeSync(destinationFd);
    closeSync(sourceFd);
  }
}
