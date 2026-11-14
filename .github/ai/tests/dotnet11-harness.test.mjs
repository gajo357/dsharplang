import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { isReviewedBase, migrateContent } from '../migrate-dotnet11-harness.mjs';

test('trusted SDK migration accepts both reviewed event bases only', () => {
  assert.equal(isReviewedBase('f22efb53fdedbb17339d798d61c9d25577497cd8'), true);
  assert.equal(isReviewedBase('5e0c1d366a9f434501ec3c5dbe32cbe65def90b3'), true);
  assert.equal(isReviewedBase('unreviewed-base'), false);
});

test('trusted SDK migration preserves content except reviewed version pins', () => {
  const original = "test assertion\n10.0.100 net10.0\n'DOTNET_CLI_HOME', 'HTTP_PROXY'\n";
  const hash = createHash('sha256').update(original).digest('hex');
  assert.equal(migrateContent(original, hash), "test assertion\n11.0.100 net11.0\n'DOTNET_CLI_HOME', 'XDG_RUNTIME_DIR', 'HTTP_PROXY'\n");
  assert.throws(() => migrateContent(original + 'changed test\n', hash), /Unexpected trusted file content/);
  const noPin = 'unchanged assertion\n';
  assert.throws(() => migrateContent(noPin, createHash('sha256').update(noPin).digest('hex')), /No reviewed version pin/);
});
