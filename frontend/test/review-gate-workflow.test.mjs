import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  return exports;
}

const reviewGateUtils = loadModule('features/thesis/utils/reviewGate.ts');

test('isPdfSubmissionFile detects PDF by extension or file name case-insensitively', () => {
  assert.equal(reviewGateUtils.isPdfSubmissionFile('de-cuong.PDF', null), true);
  assert.equal(reviewGateUtils.isPdfSubmissionFile('de-cuong', '.pdf'), true);
  assert.equal(reviewGateUtils.isPdfSubmissionFile('de-cuong.docx', '.docx'), false);
  assert.equal(reviewGateUtils.isPdfSubmissionFile(null, null), false);
});

test('formatReviewGateStudentsInline formats student names with userCode when available', () => {
  assert.equal(
    reviewGateUtils.formatReviewGateStudentsInline({
      studentNames: 'Fallback',
      students: [
        { studentId: 's1', fullName: 'Nguyễn Văn A', userCode: '22520001' },
        { studentId: 's2', fullName: 'Trần Thị B', userCode: null },
      ],
    }),
    'Nguyễn Văn A (22520001) · Trần Thị B',
  );

  assert.equal(
    reviewGateUtils.formatReviewGateStudentsInline({
      studentNames: 'Nguyễn Văn A',
      students: [],
    }),
    'Nguyễn Văn A',
  );
});

test('sortReviewGateItems sorts consistently by version, status, topicTitle, and submittedAt', () => {
  const rows = [
    {
      groupId: 'g1',
      groupCode: 'Nhóm G1',
      topicCode: 'KLTN-1',
      topicTitleVi: 'Bản đồ thông minh',
      studentNames: 'A',
      advisorName: 'GV1',
      batchName: 'Đợt 1',
      latestFileName: 'a.pdf',
      latestVersionNo: 1,
      currentStatus: 'Approved',
      submittedAtUtc: '2026-10-01T08:00:00Z',
    },
    {
      groupId: 'g2',
      groupCode: 'Nhóm G2',
      topicCode: 'KLTN-2',
      topicTitleVi: 'An toàn mạng',
      studentNames: 'B',
      advisorName: 'GV2',
      batchName: 'Đợt 1',
      latestFileName: 'b.pdf',
      latestVersionNo: 3,
      currentStatus: 'Submitted',
      submittedAtUtc: '2026-10-03T08:00:00Z',
    },
  ];

  const bySubmittedDesc = reviewGateUtils.sortReviewGateItems(rows, {
    field: 'submittedAt',
    direction: 'desc',
  });
  assert.deepEqual(
    bySubmittedDesc.map((r) => r.groupId),
    ['g2', 'g1'],
  );

  const byTitleAsc = reviewGateUtils.sortReviewGateItems(rows, {
    field: 'topicTitle',
    direction: 'asc',
  });
  assert.deepEqual(
    byTitleAsc.map((r) => r.groupId),
    ['g2', 'g1'],
  );

  const byStatusAsc = reviewGateUtils.sortReviewGateItems(rows, {
    field: 'status',
    direction: 'asc',
  });
  assert.deepEqual(
    byStatusAsc.map((r) => r.groupId),
    ['g2', 'g1'],
  );
});

test('syllabusReviewApi and midtermApi preserve enriched review row metadata', async () => {
  const createHttpStub = (item) => ({
    async get() {
      return {
        data: {
          success: true,
          data: {
            items: [item],
            totalCount: 1,
            pageNumber: 1,
            pageSize: 10,
            totalPages: 1,
            hasPreviousPage: false,
            hasNextPage: false,
          },
        },
      };
    },
  });

  const samplePayload = {
    syllabusId: 'sub-1',
    midtermReportId: 'mid-1',
    groupId: 'abcd1234-0000-0000-0000-000000000000',
    topicId: '12345678-0000-0000-0000-000000000000',
    topicTitleVi: 'Đề tài thử nghiệm',
    topicTitleEn: 'Sample Topic',
    batchId: 'batch-1',
    batchName: 'Đợt 1 2026',
    students: [{ studentId: 'st-1', userCode: '22520001', fullName: 'Nguyễn Văn A' }],
    advisor: { advisorId: 'adv-1', fullName: 'TS. Trần B' },
    currentVersionNo: 2,
    currentStatus: 'Submitted',
    latestFile: {
      id: 'file-1',
      fileName: 'report.pdf',
      fileSize: 2048,
      fileExtension: '.pdf',
      uploadedAtUtc: '2026-10-10T10:00:00Z',
    },
  };

  const archivalUtils = loadModule('features/thesis/utils/archival.ts');

  const { syllabusReviewApi } = loadModule('features/thesis/services/syllabusReviewApi.ts', {
    '@/shared/api': { httpClient: createHttpStub(samplePayload) },
    '../utils/archival': archivalUtils,
  });
  const syllabusPage = await syllabusReviewApi.getReviewQueue({ pageNumber: 1, pageSize: 10 });
  assert.equal(syllabusPage.items[0].submissionId, 'sub-1');
  assert.equal(syllabusPage.items[0].topicTitleEn, 'Sample Topic');
  assert.equal(syllabusPage.items[0].fileSize, 2048);
  assert.equal(syllabusPage.items[0].fileExtension, '.pdf');
  assert.equal(syllabusPage.items[0].students.length, 1);

  const { midtermApi } = loadModule('features/thesis/services/midtermApi.ts', {
    '@/shared/api': { httpClient: createHttpStub(samplePayload) },
    './submissionUploadHelper': { buildUploadProgressHandler: () => undefined },
    '../utils/archival': archivalUtils,
  });
  const midtermPage = await midtermApi.getMidtermReviewQueue({ pageNumber: 1, pageSize: 10 });
  assert.equal(midtermPage.items[0].submissionId, 'mid-1');
  assert.equal(midtermPage.items[0].batchId, 'batch-1');
  assert.equal(midtermPage.items[0].advisorId, 'adv-1');
  assert.equal(midtermPage.items[0].fileSize, 2048);
});

test('buildDownloadRequestConfig and API getDownloadUrl forward inline flag for PDF preview only', async () => {
  const archivalUtils = loadModule('features/thesis/utils/archival.ts');
  assert.deepEqual(archivalUtils.buildDownloadRequestConfig(), {});
  assert.deepEqual(archivalUtils.buildDownloadRequestConfig({ inline: true }), {
    params: { inline: true },
  });

  const controller = new AbortController();
  assert.deepEqual(archivalUtils.buildDownloadRequestConfig(controller.signal), {
    signal: controller.signal,
  });

  const calls = [];
  const httpStub = {
    async get(url, config) {
      calls.push({ url, config });
      return {
        data: {
          success: true,
          data: { fileId: 'f1', downloadUrl: 'https://storage.test/f1', expiresAtUtc: '2026-10-10T10:05:00Z' },
        },
      };
    },
  };

  const { syllabusReviewApi } = loadModule('features/thesis/services/syllabusReviewApi.ts', {
    '@/shared/api': { httpClient: httpStub },
    '../utils/archival': archivalUtils,
  });

  await syllabusReviewApi.getDownloadUrl('f1');
  await syllabusReviewApi.getDownloadUrl('f1', { inline: true });

  assert.deepEqual(calls, [
    { url: '/api/thesis/submission-files/f1/download', config: {} },
    { url: '/api/thesis/submission-files/f1/download', config: { params: { inline: true } } },
  ]);
});
