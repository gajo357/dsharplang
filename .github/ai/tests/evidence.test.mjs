import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

test('real TRX parser rejects absent, zero, skipped, failed and inconsistent discovery', () => {
  const root = mkdtempSync(join(tmpdir(), 'ai-trx-'));
  const file = join(root, 'results.trx');
  const check = () => spawnSync(process.execPath, [fileURLToPath(new URL('../../../scripts/check-test-results.mjs', import.meta.url)), file], {encoding:'utf8'});
  const trx = (outcome='Passed', count=1, result=true) => `<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>${result ? `<UnitTestResult testId="test-1" outcome="${outcome}"/>` : ''}</Results><ResultSummary outcome="Completed"><Counters total="${count}" executed="${count}" passed="${count}"/></ResultSummary></TestRun>`;
  try {
    assert.notEqual(check().status,0);
    writeFileSync(file,trx()); assert.equal(check().status,0);
    for (const text of [trx('Passed',0,false),trx('NotExecuted'),trx('Failed'),trx('Passed',2)]) {
      writeFileSync(file,text); assert.notEqual(check().status,0);
    }
  } finally { rmSync(root,{recursive:true,force:true}); }
});

test('XML evidence supports namespace prefixes and rejects malformed or forged reports', () => {
  const root = mkdtempSync(join(tmpdir(), 'ai-xml-'));
  const file = join(root, 'results.trx');
  const check = () => spawnSync(process.execPath, [fileURLToPath(new URL('../../../scripts/check-test-results.mjs', import.meta.url)), file], {encoding:'utf8'});
  const valid = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult testId="one" outcome="Passed"/></Results><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1"/></ResultSummary></TestRun>';
  try {
    writeFileSync(file, valid.replace(/<(\/?)([A-Za-z]+)/g, '<$1t:$2').replace('xmlns=', 'xmlns:t='));
    assert.equal(check().status, 0);
    for (const invalid of [
      valid.slice(0,-10),
      valid.replace('http://microsoft.com/schemas/VisualStudio/TeamTest/2010','urn:untrusted'),
      '<!DOCTYPE TestRun [<!ENTITY value "one">]>' + valid.replace('testId="one"','testId="&value;"'),
      valid.replace('testId="one"',''),
      valid.replace('total="1"','total="NaN"'),
      valid.replace('</Results>','<UnitTestResult testId="one" outcome="Passed"/></Results>'),
    ]) { writeFileSync(file, invalid); assert.notEqual(check().status, 0); }
  } finally { rmSync(root,{recursive:true,force:true}); }
});

test('JUnit validation requires nonzero successful tests without skips or errors', async () => {
  const { automationResults } = await import('../../../scripts/validation.mjs');
  const root = mkdtempSync(join(tmpdir(), 'ai-junit-'));
  const file = join(root, 'automation.xml');
  const valid = `<testsuites><testsuite>${Array.from({length:7},(_,i)=>`<testcase name="case-${i}"/>`).join('')}</testsuite></testsuites>`;
  try {
    writeFileSync(file, valid); assert.equal(automationResults(file).passed,7);
    for (const invalid of ['<testsuites/>', ...['skipped','failure','error'].map(tag=>valid.replace('<testcase name="case-0"/>',`<testcase name="case-0"><${tag}/></testcase>`))]) {
      writeFileSync(file, invalid); assert.throws(()=>automationResults(file));
    }
  } finally { rmSync(root,{recursive:true,force:true}); }
});
