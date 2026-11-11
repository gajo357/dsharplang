// Check repository documentation without fetching URLs or following symlinks.
import assert from 'node:assert/strict';
import { existsSync, lstatSync, readFileSync, realpathSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { files } from '../../scripts/validation.mjs';

const root = realpathSync(process.argv[2]);
const documents = [join(root, 'README.md'), ...files(join(root, 'docs')).filter(p => p.endsWith('.md'))];
const inside = path => { const rel = relative(root, path); return rel !== '..' && !rel.startsWith('../') && !isAbsolute(rel); };
assert(documents.length >= 5, 'Expected design documentation was not discovered');
let links = 0;
for (const path of documents) {
  assert(!lstatSync(path).isSymbolicLink() && inside(realpathSync(path)), `Unsafe document: ${path}`);
  let text = readFileSync(path, 'utf8');
  assert(text.trim(), `Empty documentation: ${path}`);
  assert(!/^(<<<<<<< |=======\s*$|>>>>>>> )/m.test(text), `Merge conflict: ${path}`);
  text = text.replace(/^```.*?^```[^\n]*/gms, '');
  for (const [, href] of text.matchAll(/(?<!!)\[[^\]\n]+\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g)) {
    if (/^[a-zA-Z][\w+.-]*:/.test(href) || href.startsWith('#')) continue;
    const target = resolve(dirname(path), decodeURIComponent(href.split('#')[0]));
    assert(inside(target) && existsSync(target) && inside(realpathSync(target)), `${path}: broken/unsafe link ${href}`);
    links++;
  }
}
console.log(JSON.stringify({ suite: 'documentation', files: documents.length, localLinks: links, passed: true }));
