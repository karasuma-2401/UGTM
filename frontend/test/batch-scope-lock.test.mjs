import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';
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
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency in ${path}: ${name}`);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  return exports;
}

function walk(node, predicate, results = []) {
  if (!node || typeof node !== 'object') return results;
  if (Array.isArray(node)) {
    for (const child of node) walk(child, predicate, results);
    return results;
  }
  if (predicate(node)) results.push(node);
  if (node.props) {
    if ('children' in node.props) walk(node.props.children, predicate, results);
    for (const [key, value] of Object.entries(node.props)) {
      if (key !== 'children' && value && typeof value === 'object') {
        walk(value, predicate, results);
      }
    }
  }
  return results;
}

const currentBatches = loadModule('shared/academic-periods/currentBatches.ts');

const closedBatch = {
  id: 'batch-closed',
  batchName: 'Đợt KLTN 2025 HK1',
  academicYear: '2024-2025',
  semester: 1,
  status: 'Closed',
  startDate: '2025-01-01',
  endDate: '2025-06-30',
  timeZoneId: 'Asia/Ho_Chi_Minh',
};

const draftBatch = {
  id: 'batch-draft',
  batchName: 'Đợt KLTN 2027 HK1',
  academicYear: '2026-2027',
  semester: 1,
  status: 'Draft',
  startDate: '2027-01-01',
  endDate: '2027-06-30',
  timeZoneId: 'Asia/Ho_Chi_Minh',
};

const openBatch = {
  id: 'batch-open',
  batchName: 'Đợt KLTN 2026 HK2',
  academicYear: '2025-2026',
  semester: 2,
  status: 'Open',
  startDate: '2026-01-01',
  endDate: '2099-12-31',
  timeZoneId: 'Asia/Ho_Chi_Minh',
};

const makeHasRole = (roles) => (required) =>
  (Array.isArray(required) ? required : [required]).some((role) => roles.includes(role));

test('canSelectAcademicBatch allows only Admin, AcademicAffairs, and Faculty', () => {
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['Admin'])), true);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['AcademicAffairs'])), true);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['Faculty'])), true);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['Lecturer', 'Faculty'])), true);

  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['Student'])), false);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['Lecturer'])), false);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole(['CouncilMember'])), false);
  assert.equal(currentBatches.canSelectAcademicBatch(makeHasRole([])), false);
  assert.equal(currentBatches.canSelectAcademicBatch(undefined), false);
});

test('getActiveOpenBatch picks only an Open batch and never falls back to Closed or Draft batches', () => {
  assert.equal(
    currentBatches.getActiveOpenBatch([closedBatch, draftBatch, openBatch])?.id,
    'batch-open',
  );
  assert.equal(currentBatches.getActiveOpenBatch([closedBatch, draftBatch]), null);
  assert.equal(currentBatches.getActiveOpenBatch([]), null);

  // Even if the single Open batch schedule starts slightly in the future, it is still the active Open batch.
  const futureOpen = { ...openBatch, id: 'batch-open-future', startDate: '2090-01-01' };
  assert.equal(
    currentBatches.getActiveOpenBatch([closedBatch, futureOpen], new Date('2026-10-01T00:00:00Z'))
      ?.id,
    'batch-open-future',
  );
});

test('TopicGuidancePage strips URL batchId for Lecturer while preserving it for Admin', () => {
  function renderGuidance(roles) {
    const { TopicGuidancePage } = loadModule('features/topics/pages/TopicGuidancePage.tsx', {
      'lucide-react': icons,
      'react/jsx-runtime': jsxRuntime,
      'react-router-dom': {
        useSearchParams: () => [new URLSearchParams('tab=groups&batchId=batch-closed'), () => {}],
      },
      '@/components/ui/navigation-tabs': { NavigationTabs: 'NavigationTabs' },
      './TopicProposePage': { TopicProposePage: 'TopicProposePage' },
      './TopicAdvisingPage': { TopicAdvisingPage: 'TopicAdvisingPage' },
      '@/components/ui/page-header': { PageHeader: 'PageHeader' },
      '@/shared/academic-periods/currentBatches': currentBatches,
      '@/shared/auth': { useAuth: () => ({ hasRole: makeHasRole(roles) }) },
      '../types/advising.types': loadModule('features/topics/types/advising.types.ts'),
      '../types/topic.types': loadModule('features/topics/types/topic.types.ts'),
    });
    return TopicGuidancePage();
  }

  const lecturerTree = renderGuidance(['Lecturer']);
  const lecturerPane = walk(lecturerTree, (n) => n.type === 'TopicAdvisingPage')[0];
  assert.equal(lecturerPane.props.initialFilters.batchId, '');

  const adminTree = renderGuidance(['Admin']);
  const adminPane = walk(adminTree, (n) => n.type === 'TopicAdvisingPage')[0];
  assert.equal(adminPane.props.initialFilters.batchId, 'batch-closed');
});

test('RenameReviewPage hides batch dropdown and scopes requests to the open batch for Lecturer, but shows dropdown for Faculty', () => {
  const requests = [
    {
      id: 'req-open',
      groupId: 'g-1',
      requestedByStudentId: 'sv-1',
      oldTitleVi: 'Cũ',
      newTitleVi: 'Mới đợt đang mở',
      reason: 'Lý do',
      status: 'Created',
      createdAt: '2026-11-10T08:00:00Z',
      batchId: openBatch.id,
      batchName: openBatch.batchName,
      advisorId: 'adv-1',
      advisorName: 'GV A',
      members: [],
    },
    {
      id: 'req-closed',
      groupId: 'g-2',
      requestedByStudentId: 'sv-2',
      oldTitleVi: 'Cũ lịch sử',
      newTitleVi: 'Mới đợt đã đóng',
      reason: 'Lý do',
      status: 'Created',
      createdAt: '2025-05-10T08:00:00Z',
      batchId: closedBatch.id,
      batchName: closedBatch.batchName,
      advisorId: 'adv-1',
      advisorName: 'GV A',
      members: [],
    },
  ];

  function renderRename(roles, batches = [closedBatch, openBatch]) {
    const states = [];
    let cursor = 0;
    const mockReact = {
      ...React,
      useState: (initial) => {
        const idx = cursor++;
        if (!(idx in states)) states[idx] = typeof initial === 'function' ? initial() : initial;
        return [states[idx], (v) => (states[idx] = typeof v === 'function' ? v(states[idx]) : v)];
      },
      useMemo: (factory) => factory(),
      useEffect: () => {},
      useCallback: (fn) => fn,
    };

    const renameTypes = loadModule('features/topics/types/renameRequest.types.ts', {
      '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
    });
    const pageModule = loadModule('features/topics/pages/RenameReviewPage.tsx', {
      react: mockReact,
      'react/jsx-runtime': jsxRuntime,
      '@tanstack/react-query': {
        useQuery: () => ({
          data: requests,
          isPending: false,
          isFetching: false,
          isError: false,
          refetch: async () => {},
        }),
        useMutation: () => ({ isPending: false, isError: false, error: null, reset() {}, mutate() {} }),
        useQueryClient: () => ({ invalidateQueries: async () => {} }),
      },
      'react-router-dom': {
        useNavigate: () => () => {},
        useSearchParams: () => [new URLSearchParams(), () => {}],
      },
      'lucide-react': icons,
      '@/components/ui/avatar-group': { AvatarGroup: 'AvatarGroup' },
      '@/components/ui/button': { Button: 'Button' },
      '@/components/ui/card': { Card: 'Card' },
      '@/components/ui/data-table': { DataTable },
      '@/components/ui/input': { Input: 'Input' },
      '@/components/ui/list-card-header': { ListCardHeader: 'ListCardHeader' },
      '@/components/ui/navigation-tabs': { NavigationTabs: 'NavigationTabs' },
      '@/components/ui/page-header': { PageHeader: 'PageHeader' },
      '@/components/ui/select': { Select: 'Select' },
      '@/components/ui/statistics-bar': { StatisticsBar: 'StatisticsBar' },
      '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
      '@/components/ui/table-floating-bar': { TableFloatingBar: 'TableFloatingBar' },
      '@/shared/academic-periods/currentBatches': currentBatches,
      '@/shared/academic-periods/useAcademicBatches': {
        useAcademicBatches: () => ({ data: batches, isPending: false, isError: false }),
      },
      '@/shared/auth': {
        useAuth: () => ({ user: { id: 'u1', roles }, hasRole: makeHasRole(roles) }),
      },
      '@/shared/notifications': { OperationToast: 'OperationToast' },
      '../components/RenameReviewDialog': { RenameReviewDialog: 'RenameReviewDialog' },
      '../renameReview': loadModule('features/topics/renameReview.ts'),
      '../services/renameRequestApi': { renameRequestApi: { list: async () => requests } },
      '../services/topicApi': { getTopicErrorMessage: String },
      '../types/renameRequest.types': renameTypes,
    });

    const entry = pageModule.RenameReviewPage({ level: 'advisor' });
    return entry.type(entry.props);
  }

  const lecturerTree = renderRename(['Lecturer']);
  assert.equal(
    walk(lecturerTree, (n) => n.type === 'Select' && n.props['aria-label'] === 'Lọc theo đợt KLTN')
      .length,
    0,
  );
  const lecturerTable = walk(lecturerTree, (n) => n.type === DataTable)[0];
  assert.deepEqual(
    lecturerTable.props.data.map((r) => r.id),
    ['req-open'],
  );

  const lecturerNoOpenTree = renderRename(['Lecturer'], [closedBatch]);
  const lecturerNoOpenTable = walk(lecturerNoOpenTree, (n) => n.type === DataTable)[0];
  assert.deepEqual(lecturerNoOpenTable.props.data, []);

  const facultyTree = renderRename(['Faculty']);
  assert.equal(
    walk(facultyTree, (n) => n.type === 'Select' && n.props['aria-label'] === 'Lọc theo đợt KLTN')
      .length,
    1,
  );
  const facultyTable = walk(facultyTree, (n) => n.type === DataTable)[0];
  assert.deepEqual(
    facultyTable.props.data.map((r) => r.id),
    ['req-open', 'req-closed'],
  );
});

test('AdvisorMilestoneSetupPage hides batch selector for non-management roles and shows it when canSelectBatch is true', () => {
  const uiDeps = {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@/lib/utils': { cn: (...c) => c.filter(Boolean).join(' ') },
    '@/components/ui/button': {
      Button: ({ children, ...p }) => React.createElement('button', p, children),
    },
    '@/components/ui/card': {
      Card: ({ children }) => React.createElement('section', null, children),
      CardHeader: ({ children }) => React.createElement('div', null, children),
      CardTitle: ({ children }) => React.createElement('h2', null, children),
    },
    '@/components/ui/inline-alert': {
      InlineAlert: ({ title, children }) =>
        React.createElement('div', { role: 'alert' }, title, children),
    },
    '@/components/ui/page-header': {
      PageHeader: ({ title, children }) => React.createElement('header', null, title, children),
    },
    '@/components/ui/select': {
      Select: ({ id }) => React.createElement('select', { id }),
    },
    '@/components/ui/statistics-bar': { StatisticsBar: () => null },
    '../components/milestone': {
      AdvisorGroupPlanCard: () => null,
      AdvisorMilestoneCreateDialog: () => null,
      AdvisorMilestoneReviewDialog: () => null,
      AdvisorMilestoneTable: () => null,
    },
  };

  const baseHook = {
    batches: [closedBatch, openBatch],
    selectedBatchId: openBatch.id,
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
  };

  const lecturerPage = loadModule('features/thesis/pages/AdvisorMilestoneSetupPage.tsx', {
    ...uiDeps,
    '../hooks/useAdvisorMilestones': {
      useAdvisorMilestones: () => ({ ...baseHook, canSelectBatch: false }),
    },
  }).AdvisorMilestoneSetupPage;
  const lecturerHtml = renderToStaticMarkup(React.createElement(lecturerPage));
  assert.doesNotMatch(lecturerHtml, /advisor-milestone-batch-select/);
  assert.match(lecturerHtml, /advisor-milestone-group-select/);

  const adminPage = loadModule('features/thesis/pages/AdvisorMilestoneSetupPage.tsx', {
    ...uiDeps,
    '../hooks/useAdvisorMilestones': {
      useAdvisorMilestones: () => ({ ...baseHook, canSelectBatch: true }),
    },
  }).AdvisorMilestoneSetupPage;
  const adminHtml = renderToStaticMarkup(React.createElement(adminPage));
  assert.match(adminHtml, /advisor-milestone-batch-select/);
});
