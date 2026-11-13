import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import * as icons from 'lucide-react';
import { DataTable } from './helpers/data-table.mjs';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

const commonUiDeps = {
  react: React,
  'react/jsx-runtime': jsxRuntime,
  'lucide-react': icons,
  '@/lib/utils': {
    cn: (...classes) => classes.filter(Boolean).join(' '),
  },
  '@/components/ui/button': {
    Button: ({ children, type = 'button', onClick, disabled, title, className }) =>
      React.createElement('button', { type, onClick, disabled, title, className }, children),
  },
  '@/components/ui/card': {
    Card: ({ children, className }) => React.createElement('section', { className }, children),
    CardHeader: ({ children, className }) => React.createElement('div', { className }, children),
    CardTitle: ({ children, className }) => React.createElement('h2', { className }, children),
  },
  '@/components/ui/badge': {
    Badge: ({ children, className }) =>
      React.createElement('span', { className }, children),
  },
  '@/components/ui/status-badge': {
    StatusBadge: ({ label }) => React.createElement('span', null, label),
  },
  '@/components/ui/avatar': {
    Avatar: ({ name }) => React.createElement('span', { 'data-avatar': name }, name.slice(0, 2)),
  },
  '@/components/ui/avatar-group': {
    AvatarGroup: ({ items = [] }) =>
      React.createElement('div', { 'data-avatar-group': 'true', 'data-count': items.length }),
  },
  '@/components/ui/rich-text-content': {
    RichTextContent: ({ content, emptyFallback }) =>
      React.createElement('div', { 'data-rich-content': 'true' }, content || emptyFallback),
    stripRichText: (content) =>
      String(content || '')
        .replace(/[*_`#-]/g, '')
        .trim(),
  },
  '@/components/ui/rich-text-editor': {
    RichTextEditor: ({ id, value, placeholder }) =>
      React.createElement('textarea', { id, defaultValue: value, placeholder }),
  },
  '@/components/ui/data-table': { DataTable },
  '@/components/ui/inline-alert': {
    InlineAlert: ({ tone, title, children }) =>
      React.createElement(
        'div',
        { role: 'alert', 'data-tone': tone },
        title && React.createElement('p', null, title),
        children,
      ),
  },
  '@/components/ui/page-header': {
    PageHeader: ({ title, description, children }) =>
      React.createElement(
        'header',
        null,
        React.createElement('h1', null, title),
        description && React.createElement('p', null, description),
        children,
      ),
  },
  '@/components/ui/select': {
    Select: ({ id, options = [], value, placeholder }) =>
      React.createElement(
        'select',
        { id, value, readOnly: true },
        React.createElement('option', { value: '' }, placeholder || ''),
        ...options.map((opt) =>
          React.createElement('option', { key: opt.value, value: opt.value }, opt.label),
        ),
      ),
  },
  '@/components/ui/statistics-bar': {
    StatisticsBar: ({ items = [] }) =>
      React.createElement(
        'div',
        { 'data-stats-bar': 'true' },
        items.map((item) =>
          React.createElement('button', { key: item.key, type: 'button' }, `${item.label}:${item.value}`),
        ),
      ),
  },
};

function loadHookHelpers() {
  const formatters = loadFormatters();
  return loadModule('features/thesis/hooks/useAdvisorMilestones.ts', {
    react: React,
    '@tanstack/react-query': {
      useQuery: () => ({}),
      useMutation: () => ({ mutateAsync: async () => ({}), reset() {}, isPending: false, error: null }),
      useQueryClient: () => ({ invalidateQueries: async () => {} }),
    },
    '@/features/topics/services/advisingApi': { advisingApi: {} },
    '@/shared/academic-periods/useAcademicBatches': { useAcademicBatches: () => ({}) },
    '@/shared/auth': { useAuth: () => ({ user: null }) },
    '@/shared/notifications': { notify: { success() {}, warning() {}, error() {} } },
    '../services/milestoneApi': { milestoneApi: {} },
    '../services/syllabusApi': { syllabusApi: {} },
    '../utils/milestoneFormatters': formatters,
    '../utils/thesisErrors': { getThesisErrorMessage: String },
  });
}

function loadFormatters() {
  return loadModule('features/thesis/utils/milestoneFormatters.ts');
}

const sampleGroup = {
  groupId: 'group-1',
  groupCode: 'NH-112233',
  topicId: 'topic-1',
  topicCode: 'DT-AABBCC',
  topicTitleVi: 'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG',
  studentNames: 'Nguyễn Minh Anh, Trần Quốc Bảo',
  status: 'InProgress',
  students: [
    { studentId: 's1', fullName: 'Nguyễn Minh Anh', userCode: '22520001' },
    { studentId: 's2', fullName: 'Trần Quốc Bảo', userCode: '22520002' },
  ],
};

const sampleMilestones = [
  {
    milestone: {
      id: 'm1',
      groupId: 'group-1',
      title: 'Mốc 1 — Đặc tả kiến trúc & ERD',
      description: '- Nộp file PDF đặc tả API\n- Push mã nguồn **FastAPI**',
      deadlineUtc: '2026-10-01T16:59:00Z',
      createdBy: 'gv1',
      createdAtUtc: '2026-09-15T08:00:00Z',
    },
    report: {
      progressReportId: 'r1',
      milestoneId: 'm1',
      groupId: 'group-1',
      currentVersionNo: 1,
      currentStatus: 'Accepted',
      versions: [
        {
          versionNo: 1,
          gitUrl: 'https://github.com/uit/thesis-v1',
          studentNote: 'Hoàn thành v1',
          submittedBy: 's1',
          submittedAtUtc: '2026-09-30T10:00:00Z',
          isLate: false,
          status: 'Accepted',
          file: {
            id: 'f1',
            fileName: 'dac-ta-kien-truc-v1.pdf',
            contentType: 'application/pdf',
            fileSize: 2457600,
            fileExtension: '.pdf',
            checksumSha256: 'abc',
            uploadedBy: 's1',
            uploadedAtUtc: '2026-09-30T10:00:00Z',
          },
          review: {
            id: 'rev1',
            versionNo: 1,
            decision: 'Accepted',
            feedback: 'Kiến trúc rõ ràng, đạt yêu cầu.',
            reviewedBy: 'gv1',
            reviewedAtUtc: '2026-10-01T09:00:00Z',
          },
        },
      ],
    },
  },
  {
    milestone: {
      id: 'm2',
      groupId: 'group-1',
      title: 'Mốc 2 — Pipeline GraphRAG lõi',
      description: 'Xây dựng ingestion và API truy xuất',
      deadlineUtc: '2026-11-20T16:59:00Z',
      createdBy: 'gv1',
      createdAtUtc: '2026-10-05T08:00:00Z',
    },
    report: {
      progressReportId: 'r2',
      milestoneId: 'm2',
      groupId: 'group-1',
      currentVersionNo: 2,
      currentStatus: 'Submitted',
      versions: [
        {
          versionNo: 2,
          gitUrl: 'https://github.com/uit/thesis-v2',
          studentNote: 'Đã bổ sung RAGAS benchmark',
          submittedBy: 's2',
          submittedAtUtc: '2026-11-12T10:00:00Z',
          isLate: false,
          status: 'Submitted',
          file: null,
          review: null,
        },
      ],
    },
  },
  {
    milestone: {
      id: 'm3',
      groupId: 'group-1',
      title: 'Mốc 3 — Giao diện trò chuyện & kiểm thử',
      description: null,
      deadlineUtc: '2026-12-05T16:59:00Z',
      createdBy: 'gv1',
      createdAtUtc: '2026-10-20T08:00:00Z',
    },
    report: null,
  },
];

test('computeMilestoneEligibility never triggers Gate-1 error when 0 groups exist', () => {
  const { computeMilestoneEligibility } = loadHookHelpers();

  assert.deepEqual(computeMilestoneEligibility(null, null), {
    canCreateMilestones: false,
    ineligibleReason: null,
  });

  const pendingGate1 = computeMilestoneEligibility(
    { ...sampleGroup, status: 'SyllabusSubmitted' },
    'Submitted',
  );
  assert.equal(pendingGate1.canCreateMilestones, false);
  assert.match(pendingGate1.ineligibleReason, /Cổng 1/);

  assert.deepEqual(
    computeMilestoneEligibility({ ...sampleGroup, status: 'InProgress' }, 'Approved'),
    { canCreateMilestones: true, ineligibleReason: null },
  );

  const stopped = computeMilestoneEligibility({ ...sampleGroup, status: 'Stopped' }, 'Approved');
  assert.equal(stopped.canCreateMilestones, false);
  assert.match(stopped.ineligibleReason, /dừng thực hiện/);

  const finalSubmitted = computeMilestoneEligibility(
    { ...sampleGroup, status: 'FinalReportSubmitted' },
    'Approved',
  );
  assert.equal(finalSubmitted.canCreateMilestones, false);
  assert.match(finalSubmitted.ineligibleReason, /cuối kỳ/);
});

test('resolveDefaultBatchId, computeMilestoneStats, and filterMilestonesByStatus behave accurately', () => {
  const { resolveDefaultBatchId, computeMilestoneStats, filterMilestonesByStatus } =
    loadHookHelpers();

  assert.equal(
    resolveDefaultBatchId([
      { id: 'b-closed', status: 'Closed' },
      { id: 'b-open', status: 'Open' },
    ]),
    'b-open',
  );

  const stats = computeMilestoneStats(sampleMilestones);
  assert.deepEqual(stats, {
    total: 3,
    accepted: 1,
    submitted: 1,
    needRevision: 0,
    notSubmitted: 1,
  });

  assert.equal(filterMilestonesByStatus(sampleMilestones, 'All').length, 3);
  assert.equal(filterMilestonesByStatus(sampleMilestones, 'Submitted').length, 1);
  assert.equal(filterMilestonesByStatus(sampleMilestones, 'NotSubmitted')[0].milestone.id, 'm3');
});

test('milestoneFormatters computes deadline states and blocks unsafe URLs', () => {
  const { getDeadlineInfo, getSafeHttpUrl, formatFileSize } = loadFormatters();

  const now = new Date('2026-11-13T00:00:00Z').getTime();
  assert.equal(
    getDeadlineInfo('2026-11-10T00:00:00Z', 'NotSubmitted', now).urgency,
    'overdue',
  );
  assert.equal(
    getDeadlineInfo('2026-11-10T00:00:00Z', 'Accepted', now).urgency,
    'completed',
  );
  assert.equal(
    getDeadlineInfo('2026-11-14T12:00:00Z', 'NotSubmitted', now).urgency,
    'soon',
  );
  assert.equal(
    getDeadlineInfo('2026-11-10T00:00:00Z', 'Submitted', now).label,
    'Đang chờ nhận xét',
  );

  assert.equal(getSafeHttpUrl('https://github.com/uit/repo'), 'https://github.com/uit/repo');
  assert.equal(getSafeHttpUrl('javascript:alert(1)'), null);
  assert.equal(formatFileSize(2048), '2.0 KB');
});

test('AdvisorMilestoneTable uses DataTable without misleading edit labels', () => {
  const formatters = loadFormatters();
  const statusBadge = loadModule('features/thesis/components/milestone/MilestoneStatusBadge.tsx', {
    ...commonUiDeps,
    '../../types/milestone.types': {
      PROGRESS_REPORT_STATUS_LABELS: {
        NotSubmitted: 'Chưa nộp',
        Submitted: 'Chờ GV đánh giá',
        NeedRevision: 'Cần chỉnh sửa',
        Accepted: 'Đạt',
      },
    },
  });
  const { AdvisorMilestoneTable } = loadModule(
    'features/thesis/components/milestone/AdvisorMilestoneTable.tsx',
    {
      ...commonUiDeps,
      '../../utils/milestoneFormatters': formatters,
      './MilestoneStatusBadge': statusBadge,
    },
  );

  const html = renderToStaticMarkup(
    React.createElement(AdvisorMilestoneTable, {
      milestones: sampleMilestones,
      selectedMilestoneId: 'm2',
      onSelectMilestone() {},
      onOpenReview() {},
      canCreate: true,
    }),
  );

  assert.match(html, /Mốc 1 — Đặc tả kiến trúc &amp; ERD/);
  assert.match(html, /Đánh giá/);
  assert.match(html, /Xem/);
  assert.match(html, /Phiên bản v2/);
  assert.doesNotMatch(html, /Sửa deadline/);
  assert.doesNotMatch(html, /Chỉnh sửa mốc/);
});

test('AdvisorGroupPlanCard renders progress bar, members, and selected milestone summary', () => {
  const formatters = loadFormatters();
  const advisingTypes = loadModule('features/topics/types/advising.types.ts', {});
  const { AdvisorGroupPlanCard } = loadModule(
    'features/thesis/components/milestone/AdvisorGroupPlanCard.tsx',
    {
      ...commonUiDeps,
      '@/features/topics/types/advising.types': advisingTypes,
      '../../utils/milestoneFormatters': formatters,
    },
  );

  const html = renderToStaticMarkup(
    React.createElement(AdvisorGroupPlanCard, {
      selectedGroup: sampleGroup,
      selectedMilestone: sampleMilestones[0],
      stats: { total: 3, accepted: 1, submitted: 1, needRevision: 0, notSubmitted: 1 },
      onOpenReview() {},
    }),
  );

  assert.match(html, /NH-112233/);
  assert.match(html, /Đang thực hiện/);
  assert.match(html, /1\/3 mốc đạt \(33%\)/);
  assert.match(html, /Nguyễn Minh Anh/);
  assert.match(html, /22520001/);
  assert.match(html, /dac-ta-kien-truc-v1\.pdf/);
});

test('AdvisorMilestoneSetupPage shows honest empty state for 0 groups and avoids false Gate-1 error', () => {
  const mockCreateModal = { AdvisorMilestoneCreateDialog: () => null };
  const mockReviewModal = { AdvisorMilestoneReviewDialog: () => null };
  const mockTable = {
    AdvisorMilestoneTable: () => React.createElement('div', { 'data-milestone-table': 'true' }),
  };
  const mockPlan = {
    AdvisorGroupPlanCard: () => React.createElement('div', { 'data-group-plan': 'true' }),
  };

  const { AdvisorMilestoneSetupPage } = loadModule(
    'features/thesis/pages/AdvisorMilestoneSetupPage.tsx',
    {
      ...commonUiDeps,
      '../components/milestone': {
        ...mockTable,
        ...mockPlan,
        ...mockCreateModal,
        ...mockReviewModal,
      },
      '../hooks/useAdvisorMilestones': {
        useAdvisorMilestones: () => ({
          batches: [{ id: 'b1', batchName: 'Khóa luận tốt nghiệp HK2', status: 'Open' }],
          selectedBatchId: 'b1',
          setSelectedBatchId() {},
          batchesLoading: false,
          advisedGroups: [],
          selectedGroup: null,
          selectedGroupId: null,
          setSelectedGroupId() {},
          groupsLoading: false,
          topicsError: null,
          milestones: [],
          filteredMilestones: [],
          stats: { total: 0, notSubmitted: 0, submitted: 0, accepted: 0, needRevision: 0 },
          statusFilter: 'All',
          setStatusFilter() {},
          selectedMilestone: null,
          selectedMilestoneId: null,
          selectMilestone() {},
          milestonesLoading: false,
          isFetching: false,
          milestonesError: null,
          canCreateMilestones: false,
          ineligibleReason: null,
          createDialogOpen: false,
          openCreateDialog() {},
          closeCreateDialog() {},
          createMilestone: async () => ({}),
          isCreating: false,
          createError: null,
          reviewingMilestone: null,
          openReviewDialog() {},
          closeReviewDialog() {},
          reviewReport: async () => ({}),
          isReviewing: false,
          reviewError: null,
          downloadingFileId: null,
          handleDownloadFile: async () => {},
          refresh: async () => {},
        }),
      },
    },
  );

  const html = renderToStaticMarkup(React.createElement(AdvisorMilestoneSetupPage));
  assert.match(html, /Tiến độ sinh viên/);
  assert.match(html, /Chưa có nhóm hướng dẫn trong đợt này/);
  assert.doesNotMatch(html, /Cổng 1/);
  assert.doesNotMatch(html, /Sửa deadline/);
});

test('milestone dialogs integrate RichTextEditor expandable, RichTextContent, and clean version selection without suggestion popovers', () => {
  const createSrc = readFileSync(
    new URL('../src/features/thesis/components/milestone/AdvisorMilestoneCreateDialog.tsx', import.meta.url),
    'utf8',
  );
  const reviewSrc = readFileSync(
    new URL('../src/features/thesis/components/milestone/AdvisorMilestoneReviewDialog.tsx', import.meta.url),
    'utf8',
  );

  assert.match(createSrc, /<RichTextEditor[\s\S]*expandable/);
  assert.doesNotMatch(createSrc, /<RichTextEditor[\s\S]*maxLength=\{/);
  assert.doesNotMatch(createSrc, /MilestonePreset/);
  assert.doesNotMatch(createSrc, /STAGE_TITLE_SUGGESTIONS/);
  assert.match(reviewSrc, /<RichTextContent/);
  assert.match(reviewSrc, /<RichTextEditor[\s\S]*expandable/);
  assert.doesNotMatch(reviewSrc, /MilestonePreset/);
  assert.match(reviewSrc, /<Select<string>/);
  assert.doesNotMatch(reviewSrc, /Phiên bản v/);
});

test('milestone formatters compute deadline summary and submission delta accurately', () => {
  const { formatDeadlineSummary, formatSubmissionDelta } = loadFormatters();
  const baseNow = new Date(2026, 10, 14, 10, 0, 0).getTime();

  const futureSummary = formatDeadlineSummary('2026-11-21T23:59:00', baseNow);
  assert.ok(futureSummary);
  assert.equal(futureSummary.validFuture, true);
  assert.match(futureSummary.summary, /Còn 7 ngày/);

  const pastSummary = formatDeadlineSummary('2026-11-12T23:59:00', baseNow);
  assert.ok(pastSummary);
  assert.equal(pastSummary.validFuture, false);
  assert.match(pastSummary.summary, /Thời điểm này đã trôi qua/);

  const onTime = formatSubmissionDelta('2026-11-12T14:47:00Z', '2026-11-11T14:47:00Z');
  assert.equal(onTime.isLate, false);
  assert.equal(onTime.label, 'Nộp sớm 1 ngày');

  const lateHours = formatSubmissionDelta('2026-11-12T10:00:00Z', '2026-11-12T15:00:00Z');
  assert.equal(lateHours.isLate, true);
  assert.match(lateHours.label, /Trễ 5 giờ/);
});
