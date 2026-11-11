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
  const check = () => spawnSync('python3', [fileURLToPath(new URL('../../../scripts/check-test-results.py', import.meta.url)), file], {encoding:'utf8'});
  const trx = (outcome='Passed', count=1, result=true) => `<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>${result ? `<UnitTestResult testId="test-1" outcome="${outcome}"/>` : ''}</Results><ResultSummary outcome="Completed"><Counters total="${count}" executed="${count}" passed="${count}"/></ResultSummary></TestRun>`;
  try {
    assert.notEqual(check().status,0);
    writeFileSync(file,trx()); assert.equal(check().status,0);
    for (const text of [trx('Passed',0,false),trx('NotExecuted'),trx('Failed'),trx('Passed',2)]) {
      writeFileSync(file,text); assert.notEqual(check().status,0);
    }
  } finally { rmSync(root,{recursive:true,force:true}); }
});
