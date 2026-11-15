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

test('validateArchivalFile enforces extension, non-empty size, and 20 MiB limit', () => {
  const { validateArchivalFile, ARCHIVAL_MAX_FILE_SIZE_BYTES } = loadTsModule(
    '../src/features/thesis/utils/archival.ts',
  );

  assert.equal(validateArchivalFile({ name: 'report.PDF', size: 1024 }, '.pdf'), null);
  assert.equal(validateArchivalFile({ name: 'report.docx', size: 2048 }, '.docx'), null);
  assert.match(
    validateArchivalFile({ name: 'report.docx', size: 1024 }, '.pdf') ?? '',
    /\.PDF/,
  );
  assert.match(
    validateArchivalFile({ name: 'report.pdf', size: 0 }, '.pdf') ?? '',
    /rỗng/i,
  );
  assert.match(
    validateArchivalFile(
      { name: 'report.pdf', size: ARCHIVAL_MAX_FILE_SIZE_BYTES + 1 },
      '.pdf',
    ) ?? '',
    /20 MiB/,
  );
});

test('validateArchivalMetadata validates both bilingual abstracts and keywords', () => {
  const { validateArchivalMetadata } = loadTsModule(
    '../src/features/thesis/utils/archival.ts',
  );

  const validResult = validateArchivalMetadata({
    abstractVi: 'Tóm tắt tiếng Việt',
    abstractEn: 'English abstract',
    keywordsVi: 'học sâu; y tế',
    keywordsEn: 'deep learning; healthcare',
  });
  assert.deepEqual(validResult, {
    valid: true,
    abstractsReady: true,
    keywordsReady: true,
  });

  const missingKeywordResult = validateArchivalMetadata({
    abstractVi: 'Tóm tắt tiếng Việt',
    abstractEn: 'English abstract',
    keywordsVi: '   ',
    keywordsEn: 'deep learning',
  });
  assert.deepEqual(missingKeywordResult, {
    valid: false,
    abstractsReady: true,
    keywordsReady: false,
  });
});

test('computeArchivalStats, mergeArchivalSummaryRows, and filterArchivalRows support Faculty workflow', () => {
  const {
    computeArchivalStats,
    mergeArchivalSummaryRows,
    filterArchivalRows,
  } = loadTsModule('../src/features/thesis/utils/archival.ts');

  const summaries = [
    {
      id: 'sub-1',
      batchId: 'batch-1',
      groupId: 'grp-1',
      topicTitle: 'Đề tài fallback 1',
      status: 'AdvisorConfirmed',
      submissionVersion: 1,
      currentPackageVersion: 1,
      submittedAt: '2026-11-15T08:00:00Z',
      lastDecisionAt: '2026-11-15T09:00:00Z',
    },
    {
      id: 'sub-2',
      batchId: 'batch-1',
      groupId: 'grp-2',
      topicTitle: 'Đề tài 2',
      status: 'Submitted',
      submissionVersion: 1,
      currentPackageVersion: 1,
      submittedAt: '2026-11-15T08:10:00Z',
      lastDecisionAt: null,
    },
    {
      id: 'sub-3',
      batchId: 'batch-1',
      groupId: 'grp-3',
      topicTitle: 'Đề tài 3',
      status: 'NeedRevision',
      submissionVersion: 2,
      currentPackageVersion: 2,
      submittedAt: '2026-11-15T08:20:00Z',
      lastDecisionAt: '2026-11-15T09:20:00Z',
    },
    {
      id: 'sub-4',
      batchId: 'batch-1',
      groupId: 'grp-4',
      topicTitle: 'Đề tài 4',
      status: 'ArchiveConfirmed',
      submissionVersion: 1,
      currentPackageVersion: 1,
      submittedAt: '2026-11-15T08:30:00Z',
      lastDecisionAt: '2026-11-15T10:00:00Z',
    },
  ];

  assert.deepEqual(computeArchivalStats(summaries), {
    all: 4,
    advisorConfirmed: 1,
    submitted: 1,
    needRevision: 1,
    archiveConfirmed: 1,
  });

  const rows = mergeArchivalSummaryRows(
    summaries,
    [
      {
        groupId: 'grp-1',
        groupCode: 'N01',
        topicTitleVi: 'Phân tích mã nguồn LLM',
        topicTitleEn: 'LLM Code Analysis',
        studentNames: ['Nguyễn Minh Đức', 'Trần Mai Anh'],
        studentCodes: ['24521603', '24521604'],
      },
    ],
    {
      'grp-1': {
        groupId: 'grp-1',
        groupCode: 'N01',
        topicTitleVi: 'Phân tích mã nguồn LLM',
        topicTitleEn: 'LLM Code Analysis',
        advisorName: 'TS. Trần Hoàng Long',
        students: [
          { studentCode: '24521603', fullName: 'Nguyễn Minh Đức' },
          { studentCode: '24521604', fullName: 'Trần Mai Anh' },
        ],
      },
    },
  );

  assert.equal(rows[0].groupCode, 'N01');
  assert.equal(rows[0].advisorName, 'TS. Trần Hoàng Long');
  assert.deepEqual(rows[0].studentNames, ['Nguyễn Minh Đức', 'Trần Mai Anh']);
  assert.equal(rows[0].statusLabel, 'Chờ nghiệm thu');

  assert.equal(filterArchivalRows(rows, 'AdvisorConfirmed', '').length, 1);
  assert.equal(filterArchivalRows(rows, 'ALL', '').length, 4);
  assert.equal(filterArchivalRows(rows, 'ALL', 'Hoàng Long').length, 1);
  assert.equal(filterArchivalRows(rows, 'NeedRevision', '24521603').length, 0);
});

test('getArchivalStatusDisplay and formatArchivalBlockingReason map backend statuses and error codes', () => {
  const { getArchivalStatusDisplay, formatArchivalBlockingReason } = loadTsModule(
    '../src/features/thesis/utils/archival.ts',
  );

  assert.equal(getArchivalStatusDisplay('AdvisorConfirmed').label, 'Chờ nghiệm thu');
  assert.equal(getArchivalStatusDisplay('ArchiveConfirmed').tone, 'success');
  assert.equal(getArchivalStatusDisplay(null).label, 'Chưa nộp');
  assert.match(
    formatArchivalBlockingReason('FinalSubmission.DefenseNotLocked'),
    /Locked/,
  );
});

test('mergeAdvisorArchivalRows resolves group metadata from advisingGroups and StudentGroupDetailDto', () => {
  const { mergeAdvisorArchivalRows } = loadTsModule('../src/features/thesis/utils/archival.ts');

  const summaries = [
    {
      id: 'sub-1',
      submissionId: 'sub-1',
      batchId: 'batch-1',
      groupId: 'grp-1',
      topicTitle: 'Đề tài fallback',
      status: 'Submitted',
      submissionVersion: 1,
      currentVersionNo: 1,
      submittedAt: '2026-11-15T08:00:00Z',
      advisorConfirmedAt: null,
      archivedConfirmedAt: null,
    },
  ];

  const rows = mergeAdvisorArchivalRows(
    summaries,
    [
      {
        groupId: 'grp-1',
        topicId: 'top-1',
        titleVi: 'Đề tài Advising',
        titleEn: 'Advising Thesis',
        batchId: 'batch-1',
        batchName: 'Đợt 1',
        status: 'Defended',
        createdAt: '2026-11-01T00:00:00Z',
        confirmedStudentsCount: 2,
        pendingConfirmationsCount: 0,
        expiredInvitationsCount: 0,
        students: [
          {
            studentId: 's1',
            userCode: '24521603',
            fullName: 'Nguyễn Minh Đức',
            confirmationStatus: 'Confirmed',
            joinedAt: '2026-11-01T00:00:00Z',
            confirmedAtUtc: '2026-11-01T00:00:00Z',
            confirmationExpiresAtUtc: null,
          },
          {
            studentId: 's2',
            userCode: '24521604',
            fullName: 'Trần Mai Anh',
            confirmationStatus: 'Pending',
            joinedAt: '2026-11-01T00:00:00Z',
            confirmedAtUtc: null,
            confirmationExpiresAtUtc: null,
          },
        ],
      },
    ],
    {
      'grp-1': {
        groupId: 'grp-1',
        groupCode: 'N01',
        topicId: 'top-1',
        topicCode: 'KLTN-2026-084',
        topicTitleVi: 'Đề tài Chi tiết',
        batchName: 'Đợt 1',
        members: [
          {
            studentId: 's1',
            userCode: '24521603',
            fullName: 'Nguyễn Minh Đức',
            email: 'duc@uit.edu.vn',
            confirmationStatus: 'Confirmed',
          },
          {
            studentId: 's2',
            userCode: '24521604',
            fullName: 'Trần Mai Anh',
            email: 'anh@uit.edu.vn',
            confirmationStatus: 'Confirmed',
          },
        ],
        advisor: {
          advisorId: 'adv-1',
          fullName: 'Trần Hoàng Long',
          academicTitle: 'TS.',
        },
      },
    },
  );

  assert.equal(rows[0].groupCode, 'N01');
  assert.equal(rows[0].topicTitleVi, 'Đề tài Chi tiết');
  assert.equal(rows[0].topicTitleEn, 'Advising Thesis');
  assert.deepEqual(rows[0].studentNames, ['Nguyễn Minh Đức', 'Trần Mai Anh']);
  assert.deepEqual(rows[0].studentCodes, ['24521603', '24521604']);
  assert.equal(rows[0].advisorName, 'Trần Hoàng Long');
  assert.equal(rows[0].statusLabel, 'Chờ GVHD xác nhận');
});

test('navigationConfig exposes Student, Lecturer and Faculty archival routes under progress section', () => {
  const { navigationSections } = loadTsModule('../src/shared/layout/navigationConfig.ts', {
    'lucide-react': new Proxy({}, { get: () => () => null }),
  });

  const progress = navigationSections.find((section) => section.id === 'progress');
  assert.ok(progress);

  const studentItem = progress.items.find((item) => item.path === '/thesis/archival');
  const facultyItem = progress.items.find((item) => item.path === '/thesis/review/archival');
  const advisorItem = progress.items.find((item) => item.path === '/advisor/archival');

  assert.deepEqual(studentItem?.roles, ['Student']);
  assert.equal(studentItem?.label, 'Hồ sơ lưu chiểu');
  assert.deepEqual(facultyItem?.roles, ['Faculty', 'Admin']);
  assert.equal(facultyItem?.label, 'Duyệt hồ sơ lưu chiểu');
  assert.deepEqual(advisorItem?.roles, ['Lecturer', 'Admin']);
  assert.equal(advisorItem?.label, 'Xác nhận lưu chiểu');
});
