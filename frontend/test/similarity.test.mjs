import test from 'node:test';
import assert from 'node:assert/strict';

import {
  buildSimilarityRowView,
  computeSimilarityStats,
  filterSimilarityRows,
  formatSimilarityPercent,
  normalizeSimilarityPercent,
  resolveSimilarityGroupCode,
  sortSimilarityRows,
} from '../src/features/thesis/utils/similarity.ts';

test('normalizeSimilarityPercent converts 0..1 ratio and 0..100 percentage consistently', () => {
  assert.equal(normalizeSimilarityPercent(0.185), 18.5);
  assert.equal(normalizeSimilarityPercent(8.6), 8.6);
  assert.equal(normalizeSimilarityPercent(0), 0);
  assert.equal(normalizeSimilarityPercent(120), 100);
  assert.equal(formatSimilarityPercent(0.246), '24,6%');
});

test('resolveSimilarityGroupCode maps UUID suffixes without prefix collision', () => {
  assert.equal(
    resolveSimilarityGroupCode('b2222222-2222-2222-2222-000000000301'),
    'NH-0301',
  );
  assert.equal(
    resolveSimilarityGroupCode('99999999-9999-9999-9999-999999990001'),
    'NH-0001',
  );
  assert.equal(
    resolveSimilarityGroupCode('ff378a4f-3e31-4ea2-ad14-4273cc5fbf1c'),
    'NH-FF37',
  );
});

test('computeSimilarityStats, filterSimilarityRows, and sortSimilarityRows work across statuses', () => {
  const rows = [
    buildSimilarityRowView({
      groupId: 'b2222222-2222-2222-2222-000000000301',
      finalReportId: 'r1',
      versionNo: 1,
      topicTitle: 'eBPF Anomaly',
      batchId: 'b1',
      scanStatus: 'Completed',
      similarityCoverage: 0.084,
      citedFindings: 2,
      uncitedFindings: 0,
      suspectedFindings: 0,
    }),
    buildSimilarityRowView({
      groupId: 'b2222222-2222-2222-2222-000000000303',
      finalReportId: 'r2',
      versionNo: 2,
      topicTitle: 'AWQ Quantization',
      batchId: 'b1',
      students: [{ id: 's1', fullName: 'Trần Minh Khôi', userCode: '22520777' }],
      scanStatus: 'Completed',
      similarityCoverage: 0.468,
      citedFindings: 1,
      uncitedFindings: 3,
      suspectedFindings: 2,
    }),
    buildSimilarityRowView({
      groupId: 'b2222222-2222-2222-2222-000000000304',
      finalReportId: 'r3',
      versionNo: 1,
      topicTitle: 'Android Malware GNN',
      batchId: 'b2',
      scanStatus: 'Processing',
    }),
    buildSimilarityRowView({
      groupId: 'b2222222-2222-2222-2222-000000000302',
      finalReportId: 'r4',
      versionNo: 1,
      topicTitle: 'Federated Medical',
      batchId: 'b1',
      scanStatus: null,
    }),
  ];

  const stats = computeSimilarityStats(rows);
  assert.deepEqual(stats, {
    total: 4,
    needsAttention: 1,
    lowSimilarity: 1,
    pending: 1,
    issueOrUnscanned: 1,
  });

  const needsAttention = filterSimilarityRows(rows, {
    batchId: 'b1',
    filterKey: 'NeedsAttention',
    keyword: '22520777',
  });
  assert.equal(needsAttention.length, 1);
  assert.equal(needsAttention[0].finalReportId, 'r2');

  const sortedByCoverageDesc = sortSimilarityRows(rows, {
    field: 'coverage',
    direction: 'desc',
  });
  assert.deepEqual(
    sortedByCoverageDesc.map((r) => r.finalReportId),
    ['r2', 'r1', 'r3', 'r4'],
  );
});
