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

test('mergeScoreAndAcademicRows classifies all score and archival states accurately', () => {
  const { mergeScoreAndAcademicRows } = loadTsModule(
    '../src/features/academic/utils/scoreSummaryFormatting.ts',
  );

  const scoreItems = [
    {
      groupId: 'g-pub',
      topicTitle: 'Đề tài 1',
      summaryId: 's-1',
      summaryVersion: 5,
      status: 'PublishedToStudent',
      finalScore: 8.75,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: false,
      isReadyForPublication: false,
      calculatedAt: '2026-11-10T01:00:00Z',
      publishedAt: '2026-11-10T02:00:00Z',
    },
    {
      groupId: 'g-ready',
      topicTitle: 'Đề tài 2',
      summaryId: 's-2',
      summaryVersion: 3,
      status: 'Calculated',
      finalScore: 7.83,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: true,
      isReadyForPublication: true,
      calculatedAt: '2026-11-10T01:00:00Z',
      publishedAt: null,
    },
    {
      groupId: 'g-stale',
      topicTitle: 'Đề tài 3',
      summaryId: 's-3',
      summaryVersion: 2,
      status: 'Calculated',
      finalScore: 7.5,
      isLegacy: false,
      isStale: true,
      isReadyForCalculation: true,
      isReadyForPublication: false,
      calculatedAt: '2026-11-10T01:00:00Z',
      publishedAt: null,
    },
    {
      groupId: 'g-calc',
      topicTitle: 'Đề tài 4',
      summaryId: null,
      summaryVersion: null,
      status: 'Pending',
      finalScore: null,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: true,
      isReadyForPublication: false,
      calculatedAt: null,
      publishedAt: null,
    },
    {
      groupId: 'g-archive',
      topicTitle: 'Đề tài 5',
      summaryId: null,
      summaryVersion: null,
      status: 'Pending',
      finalScore: null,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: false,
      isReadyForPublication: false,
      calculatedAt: null,
      publishedAt: null,
    },
  ];

  const academicItems = [
    {
      groupId: 'g-pub',
      groupCode: 'N01',
      topicId: 't-1',
      topicTitleVi: 'Đề tài 1',
      topicTitleEn: null,
      studentNames: ['Nguyễn Văn A'],
      studentCodes: ['22520001'],
      facultyName: 'Khoa CNTT',
      phase: 'Final',
      statusCode: 'PUBLISHED',
      statusLabel: 'Khoa đã công bố',
      detailLabel: 'Lưu chiểu đạt',
      finalScore: 8.75,
    },
    {
      groupId: 'g-ready',
      groupCode: 'N02',
      topicId: 't-2',
      topicTitleVi: 'Đề tài 2',
      topicTitleEn: null,
      studentNames: ['Trần Thị B'],
      studentCodes: ['22520002'],
      facultyName: 'Khoa CNTT',
      phase: 'Final',
      statusCode: 'READY_TO_PUBLISH',
      statusLabel: 'Chờ công bố',
      detailLabel: 'Lưu chiểu đạt',
      finalScore: 7.83,
    },
    {
      groupId: 'g-stale',
      groupCode: 'N03',
      topicId: 't-3',
      topicTitleVi: 'Đề tài 3',
      topicTitleEn: null,
      studentNames: ['Lê Văn C'],
      studentCodes: ['22520003'],
      facultyName: 'Khoa CNTT',
      phase: 'Final',
      statusCode: 'READY_TO_PUBLISH',
      statusLabel: 'Chờ công bố',
      detailLabel: 'Lưu chiểu đạt',
      finalScore: 7.5,
    },
    {
      groupId: 'g-calc',
      groupCode: 'N04',
      topicId: 't-4',
      topicTitleVi: 'Đề tài 4',
      topicTitleEn: null,
      studentNames: ['Phạm Thị D'],
      studentCodes: ['22520004'],
      facultyName: 'Khoa CNTT',
      phase: 'Final',
      statusCode: 'READY_TO_PUBLISH',
      statusLabel: 'Chờ công bố',
      detailLabel: 'Lưu chiểu đạt',
      finalScore: null,
    },
    {
      groupId: 'g-archive',
      groupCode: 'N05',
      topicId: 't-5',
      topicTitleVi: 'Đề tài 5',
      topicTitleEn: null,
      studentNames: ['Hoàng Văn E'],
      studentCodes: ['22520005'],
      facultyName: 'Khoa CNTT',
      phase: 'Final',
      statusCode: 'MISSING_ARCHIVE',
      statusLabel: 'Chưa đủ lưu chiểu',
      detailLabel: 'Chưa công bố',
      finalScore: null,
    },
  ];

  const merged = mergeScoreAndAcademicRows(scoreItems, academicItems);
  assert.equal(merged.length, 5);
  assert.deepEqual(
    merged.map((r) => [r.groupCode, r.filterKey, r.displayStatusLabel]),
    [
      ['N01', 'published', 'Đã công bố'],
      ['N02', 'readyToPublish', 'Đủ điều kiện'],
      ['N03', 'needsCalculation', 'Cần tính lại'],
      ['N04', 'needsCalculation', 'Sẵn sàng tính'],
      ['N05', 'needsAction', 'Chờ lưu chiểu'],
    ],
  );
});

test('extractPublicationFailureMessage maps per-group conflict codes to readable messages', () => {
  const { extractPublicationFailureMessage } = loadTsModule(
    '../src/features/academic/utils/scoreSummaryFormatting.ts',
  );

  const rowsByGroupId = new Map([
    ['g-1', { groupId: 'g-1', groupCode: 'N01' }],
    ['g-2', { groupId: 'g-2', groupCode: 'N02' }],
  ]);

  const msg = extractPublicationFailureMessage(
    {
      message: 'Công bố bị từ chối.',
      errors: {
        'g-1': ['ScoreSummary.Stale'],
        'g-2': ['ScoreSummary.VersionConflict'],
      },
    },
    rowsByGroupId,
  );

  assert.match(msg, /N01:/);
  assert.match(msg, /N02:/);
  assert.match(msg, /cần tính lại/);
  assert.match(msg, /Phiên bản bảng điểm/);
});

test('navigationConfig restricts /councils/results to Faculty, AcademicAffairs, and Admin', () => {
  const { navigationSections } = loadTsModule('../src/shared/layout/navigationConfig.ts', {
    'lucide-react': new Proxy({}, { get: () => () => null }),
  });

  const progressSection = navigationSections.find((section) => section.id === 'progress');
  assert.ok(progressSection);
  const resultsNav = progressSection.items.find((item) => item.path === '/councils/results');
  assert.ok(resultsNav);
  assert.equal(resultsNav.label, 'Tổng hợp kết quả');
  assert.deepEqual(resultsNav.roles, ['Faculty', 'AcademicAffairs', 'Admin']);
});
