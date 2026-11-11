import { readFileSync, writeFileSync } from 'node:fs';
const root = process.env.REPORT_DIR;
const read = (file, fallback) => { try { return JSON.parse(readFileSync(`${root}/${file}`, 'utf8')); } catch { return fallback; } };
writeFileSync(`${root}/verification.json`, JSON.stringify({
  baseSha: process.env.BASE_SHA, headSha: process.env.HEAD_SHA,
  policy: process.env.POLICY === 'success' ? 'pass' : 'blocked', checks: process.env.CHECKS,
  changedFiles: read('scope.json', {}).changedFiles,
  compiler: read('compiler.json', { attempted: false }),
}));
