import { validatePatch } from './lib.mjs';
import { readFileSync } from 'node:fs';
const policy = JSON.parse(readFileSync(new URL('./policy.json', import.meta.url)));
console.log(JSON.stringify({ changedFiles: validatePatch(process.argv[2], process.env.BASE_SHA, process.env.HEAD_SHA, policy), policy: 'pass' }));
