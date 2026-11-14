import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function loadTsModule(relativePath, dependencies = {}) {
  const source = readFileSync(new URL(relativePath, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    if (name in dependencies) {
      return { __esModule: true, ...dependencies[name] };
    }
    throw new Error(`Unexpected import: ${name}`);
  }, exports);
  return exports;
}

test('convertScoreScale10To4 and formatScale4Score calculate score / 10 * 4 rounded to 2 decimals', () => {
  const { convertScoreScale10To4, formatScale4Score } = loadTsModule(
    '../src/features/councils/utils/studentResults.ts',
  );

  assert.equal(convertScoreScale10To4(8.5), 3.4);
  assert.equal(convertScoreScale10To4(7.83), 3.13);
  assert.equal(convertScoreScale10To4(10), 4);
  assert.equal(convertScoreScale10To4(null), null);
  assert.equal(formatScale4Score(8.5), '3.40');
  assert.equal(formatScale4Score(7.83), '3.13');
  assert.equal(formatScale4Score(undefined), '—');
});

test('buildStudentConditionRows reflects locked, archival pending, and published states accurately', () => {
  const { buildStudentConditionRows } = loadTsModule(
    '../src/features/councils/utils/studentResults.ts',
  );

  const lockedRows = buildStudentConditionRows(
    {
      kind: 'locked',
      code: 'ScoreSummary.NotYetPublished',
      message: 'Điểm của nhóm chưa được công bố chính thức.',
    },
    null,
  );
  assert.equal(lockedRows[0].statusLabel, 'Chờ');
  assert.equal(lockedRows[0].detailLabel, 'Chưa công bố');
  assert.equal(lockedRows[1].statusLabel, 'Điểm còn khóa');
  assert.equal(lockedRows[1].detailLabel, 'Chưa hoàn thành');

  const archivalWaitingRows = buildStudentConditionRows(
    {
      kind: 'locked',
      code: 'ScoreSummary.NotYetPublished',
      message: 'Điểm của nhóm chưa được công bố chính thức.',
    },
    { status: 'AdvisorConfirmed' },
  );
  assert.equal(archivalWaitingRows[1].statusLabel, 'Chờ Khoa duyệt');
  assert.equal(archivalWaitingRows[1].statusTone, 'info');

  const publishedRows = buildStudentConditionRows(
    {
      kind: 'published',
      data: {
        groupId: 'g-1',
        topicTitle: 'Đề tài test',
        status: 'PublishedToStudent',
        publishedAt: '2026-11-14T10:00:00Z',
        finalScore: 8.75,
        calculationFormula: 'WeightedAverage',
        components: [],
      },
    },
    null,
  );
  assert.equal(publishedRows[0].statusLabel, 'Đạt');
  assert.equal(publishedRows[1].statusLabel, 'Đạt');
  assert.equal(publishedRows[0].detailLabel, 'Đã công bố');
  assert.equal(publishedRows[1].detailLabel, 'Đã hoàn thành');
});

test('replaceInterpunctWithHyphen removes middle dot separators cleanly', () => {
  const { replaceInterpunctWithHyphen } = loadTsModule(
    '../src/features/councils/utils/studentResults.ts',
  );

  assert.equal(replaceInterpunctWithHyphen('Đợt 1 · HK1 2026-2027'), 'Đợt 1 - HK1 2026-2027');
  assert.equal(replaceInterpunctWithHyphen(null), '');
});

test('navigationConfig exposes Kết quả on /councils/results for Student', () => {
  const { navigationSections } = loadTsModule('../src/shared/layout/navigationConfig.ts', {
    'lucide-react': new Proxy({}, { get: () => () => null }),
  });

  const progressSection = navigationSections.find((section) => section.id === 'progress');
  assert.ok(progressSection);
  const studentNav = progressSection.items.find(
    (item) => item.path === '/councils/results' && item.label === 'Kết quả',
  );
  assert.ok(studentNav);
  assert.deepEqual(studentNav.roles, ['Student']);
});
