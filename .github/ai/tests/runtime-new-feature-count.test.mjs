import assert from 'node:assert/strict';
import { test } from 'node:test';
import { assertNewFeatureDiscovered } from '../verify-runtime.mjs';

test('changed runtime source requires a newly discovered passing feature test', () => {
  // An empty or comment-only new file leaves discovery at the prior count.
  assert.throws(() => assertNewFeatureDiscovered(3, 3), /newly discovered/);
  assert.throws(() => assertNewFeatureDiscovered(0, 0), /newly discovered/);
  assert.throws(() => assertNewFeatureDiscovered(3, 2), /newly discovered/);
  assert.throws(() => assertNewFeatureDiscovered(-1, 1), /newly discovered/);
  assert.throws(() => assertNewFeatureDiscovered(1, NaN), /newly discovered/);
  assert.doesNotThrow(() => assertNewFeatureDiscovered(3, 4));
  assert.doesNotThrow(() => assertNewFeatureDiscovered(0, 1));
});
