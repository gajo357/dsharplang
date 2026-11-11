import { compilerResults } from './validation.mjs';
console.log(JSON.stringify(compilerResults(process.argv[2])));
