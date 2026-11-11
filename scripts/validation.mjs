import assert from 'node:assert/strict';
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { SaxesParser } from 'saxes';

export function files(root) {
  const found = [];
  function visit(path) {
    const stat = lstatSync(path);
    assert(!stat.isSymbolicLink(), `Symlink: ${path}`);
    if (stat.isDirectory()) {
      for (const entry of readdirSync(path).sort()) {
        if (!['bin', 'obj', 'node_modules', '.git'].includes(entry)) visit(join(path, entry));
      }
    } else if (stat.isFile()) found.push(path);
  }
  try { visit(root); } catch (error) { if (error.code !== 'ENOENT') throw error; }
  return found;
}

export function readXml(path) {
  assert(!lstatSync(path).isSymbolicLink(), 'Symlink report');
  assert(lstatSync(path).size <= 512000, 'Oversized report');
  const parser = new SaxesParser({ xmlns: true });
  const stack = [];
  let root;
  parser.on('doctype', () => { throw new Error('DOCTYPE is not permitted in test evidence'); });
  parser.on('opentag', tag => {
    const node = { name: tag.local, namespace: tag.uri, attributes: Object.fromEntries(Object.values(tag.attributes).map(a => [a.name, a.value])), children: [] };
    if (stack.length) stack.at(-1).children.push(node);
    else { assert(!root, 'Multiple XML roots'); root = node; }
    stack.push(node);
  });
  parser.on('closetag', () => stack.pop());
  parser.write(readFileSync(path, 'utf8')).close();
  assert(root && !stack.length, 'Incomplete XML document');
  return root;
}

export function descendants(root, name, namespace) {
  return [root, ...root.children.flatMap(child => descendants(child))]
    .filter(node => (!name || node.name === name) && (namespace === undefined || node.namespace === namespace));
}

export function compilerResults(path) {
  const root = readXml(path);
  const ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010';
  assert(root.name === 'TestRun' && root.namespace === ns, 'Expected a VSTest TRX report');
  const results = descendants(root, 'UnitTestResult', ns);
  const counters = descendants(root, 'Counters', ns);
  const summary = descendants(root, 'ResultSummary', ns);
  assert(results.length && counters.length === 1, 'No tests discovered or ambiguous counters');
  const ids = results.map(r => r.attributes.testId);
  assert(ids.every(id => typeof id === 'string' && id.length) && new Set(ids).size === results.length, 'Missing/duplicate test results');
  assert(results.every(r => r.attributes.outcome === 'Passed'), 'Failed or skipped tests');
  for (const key of ['total', 'executed', 'passed']) {
    const value = counters[0].attributes[key];
    assert(/^\d+$/.test(value) && Number(value) === results.length, `Inconsistent ${key} count`);
  }
  assert(summary.length === 1 && summary[0].attributes.outcome === 'Completed', 'Incomplete test run');
  return { suite: 'compiler', total: results.length, passed: results.length, skipped: 0 };
}

export function automationResults(path) {
  const root = readXml(path);
  const cases = descendants(root, 'testcase');
  assert(cases.length >= 7, 'Expected automation tests were not discovered');
  for (const name of ['skipped', 'failure', 'error']) assert(!descendants(root, name).length, `Nonpassing required automation tests: ${name}`);
  return { suite: 'automation', total: cases.length, passed: cases.length, skipped: 0 };
}
