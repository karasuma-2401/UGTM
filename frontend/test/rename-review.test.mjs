import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { DataTable } from './helpers/data-table.mjs';

function loadModule(path, dependencies = {}, browserWindow = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', 'window', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency in ${path}: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    browserWindow,
  );
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

const sampleRequests = [
  {
    id: 'req-1',
    groupId: 'group-1',
    requestedByStudentId: 'sv-1',
    oldTitleVi: 'Hệ thống gợi ý đề tài cũ',
    newTitleVi: 'Hệ thống gợi ý đề tài đa ngôn ngữ bằng LLM',
    oldTitleEn: 'Old Recommendation System',
    newTitleEn: 'Multilingual Recommendation System with LLM',
    reason: 'Bổ sung mô hình ngôn ngữ lớn theo góp ý của GVHD.',
    status: 'Created',
    createdAt: '2026-11-10T08:00:00Z',
    batchId: 'batch-1',
    batchName: 'Khóa luận HK2',
    advisorId: 'adv-1',
    advisorName: 'TS. Nguyễn Văn An',
    members: [
      { studentId: 'sv-1', userCode: '22110001', fullName: 'Trần Minh Quân' },
    ],
  },
  {
    id: 'req-2',
    groupId: 'group-2',
    requestedByStudentId: 'sv-2',
    oldTitleVi: 'Giám sát thi trực tuyến',
    newTitleVi: 'Giám sát thi trực tuyến với Computer Vision',
    reason: 'Thu hẹp bài toán thị giác máy tính.',
    status: 'AdvisorConfirmed',
    advisorConfirmedAt: '2026-11-11T09:00:00Z',
    createdAt: '2026-11-09T08:00:00Z',
    batchId: 'batch-1',
    batchName: 'Khóa luận HK2',
    advisorId: 'adv-1',
    advisorName: 'TS. Nguyễn Văn An',
    members: [
      { studentId: 'sv-2', userCode: '22110002', fullName: 'Lê Thị Thu Hà' },
    ],
  },
  {
    id: 'req-3',
    groupId: 'group-3',
    requestedByStudentId: 'sv-3',
    oldTitleVi: 'Cổng thanh toán nội bộ',
    newTitleVi: 'Sàn thương mại điện tử đa ngành',
    reason: 'Đổi hướng sang sàn TMĐT.',
    status: 'RejectedByAdvisor',
    rejectionReason: 'Lệch khỏi mục tiêu ban đầu.',
    rejectedAt: '2026-11-08T10:00:00Z',
    createdAt: '2026-11-07T08:00:00Z',
    batchId: 'batch-2',
    batchName: 'Khóa luận HK1',
    advisorId: 'adv-2',
    advisorName: 'ThS. Lê Quang Cường',
    members: [
      { studentId: 'sv-3', userCode: '22110003', fullName: 'Phạm Hoàng Nam' },
    ],
  },
  {
    id: 'req-4',
    groupId: 'group-4',
    requestedByStudentId: 'sv-4',
    oldTitleVi: 'Phân tích log hệ thống cũ',
    newTitleVi: 'Ứng dụng phân tích log hệ thống bằng AI',
    reason: 'Chuẩn hóa thuật ngữ đề tài.',
    status: 'Completed',
    advisorConfirmedAt: '2026-11-02T08:00:00Z',
    facultyApprovedAt: '2026-11-03T08:00:00Z',
    aaApprovedAt: '2026-11-04T08:00:00Z',
    createdAt: '2026-11-01T08:00:00Z',
    batchId: 'batch-1',
    batchName: 'Khóa luận HK2',
    advisorId: 'adv-1',
    advisorName: 'TS. Nguyễn Văn An',
    members: [],
  },
  {
    id: 'req-5',
    groupId: 'group-5',
    requestedByStudentId: 'sv-5',
    oldTitleVi: 'Tối ưu PostgreSQL',
    newTitleVi: 'Tối ưu hóa hiệu năng phân tán PostgreSQL với Citus',
    reason: 'Bổ sung phạm vi thử nghiệm với Citus.',
    status: 'FacultyApproved',
    advisorConfirmedAt: '2026-11-05T08:00:00Z',
    facultyApprovedAt: '2026-11-06T08:00:00Z',
    createdAt: '2026-11-04T08:00:00Z',
    batchId: 'batch-1',
    batchName: 'Khóa luận HK2',
    advisorId: 'adv-2',
    advisorName: 'ThS. Lê Quang Cường',
    members: [
      { studentId: 'sv-5', userCode: '22110005', fullName: 'Đặng Gia Bảo' },
    ],
  },
];

const renameReviewHelpers = loadModule('features/topics/renameReview.ts');
const renameTypes = loadModule('features/topics/types/renameRequest.types.ts', {
  '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
});

test('renameRequestApi.list omits status when empty and review dispatches per-level endpoints', async () => {
  const calls = [];
  const { renameRequestApi } = loadModule('features/topics/services/renameRequestApi.ts', {
    '@/shared/api': {
      httpClient: {
        get: async (url, config) => {
          calls.push(['GET', url, config]);
          return { data: { success: true, data: sampleRequests } };
        },
        post: async (url, body) => {
          calls.push(['POST', url, body]);
          return { data: { success: true, data: sampleRequests[0] } };
        },
      },
      unwrap: (res) => res.data,
    },
    '../types/renameRequest.types': renameTypes,
  });

  const controller = new AbortController();
  const all = await renameRequestApi.list('', controller.signal);
  assert.equal(all.length, sampleRequests.length);
  assert.equal(calls[0][1], '/api/thesis/rename-requests');
  assert.equal(calls[0][2].params, undefined);
  assert.equal(calls[0][2].signal, controller.signal);

  await renameRequestApi.list('AdvisorConfirmed');
  assert.deepEqual(calls[1][2].params, { status: 'AdvisorConfirmed' });

  await renameRequestApi.review('req-1', 'advisor', { decision: 'Approve', rejectionReason: null });
  await renameRequestApi.review('req-2', 'faculty', { decision: 'Approve', rejectionReason: null });
  await renameRequestApi.review('req-5', 'academic-affairs', {
    decision: 'Reject',
    rejectionReason: 'Chưa rõ tiêu đề tiếng Anh',
  });
  assert.equal(calls[2][1], '/api/thesis/rename-requests/req-1/advisor-review');
  assert.equal(calls[3][1], '/api/thesis/rename-requests/req-2/faculty-review');
  assert.equal(calls[4][1], '/api/thesis/rename-requests/req-5/academic-affairs-review');
});

test('computeRenameQueueStats and filterAndSortRenameRequests compute counts, scopes, search, and sorting', () => {
  const stats = renameReviewHelpers.computeRenameQueueStats(sampleRequests, 'Created');
  assert.equal(stats.pendingCount, 1);
  assert.equal(stats.completedCount, 1);
  assert.equal(stats.rejectedCount, 1);
  assert.equal(stats.totalCount, 5);
  assert.deepEqual(stats.levelCounts, {
    advisor: 1,
    faculty: 1,
    'academic-affairs': 1,
  });

  const pendingOnly = renameReviewHelpers.filterAndSortRenameRequests(sampleRequests, {
    activeStatus: 'FacultyApproved',
    scope: 'pending',
    keyword: '',
    sortBy: 'createdAt',
    sortOrder: 'desc',
  });
  assert.deepEqual(pendingOnly.map((r) => r.id), ['req-5']);

  const searchByStudentCode = renameReviewHelpers.filterAndSortRenameRequests(sampleRequests, {
    activeStatus: 'Created',
    scope: 'all',
    keyword: '22110002',
    sortBy: 'createdAt',
    sortOrder: 'desc',
  });
  assert.deepEqual(searchByStudentCode.map((r) => r.id), ['req-2']);

  const batchFiltered = renameReviewHelpers.filterAndSortRenameRequests(sampleRequests, {
    activeStatus: 'Created',
    scope: 'all',
    keyword: '',
    batchId: 'batch-2',
    sortBy: 'createdAt',
    sortOrder: 'desc',
  });
  assert.deepEqual(batchFiltered.map((r) => r.id), ['req-3']);

  const sortedByTitleAsc = renameReviewHelpers.filterAndSortRenameRequests(sampleRequests, {
    activeStatus: 'Created',
    scope: 'all',
    keyword: '',
    sortBy: 'newTitleVi',
    sortOrder: 'asc',
  });
  assert.equal(sortedByTitleAsc.length, 5);
  assert.equal(sortedByTitleAsc[0].id, 'req-2');
});

test('getRenameApprovalSteps tracks 3-stage state accurately across workflow outcomes', () => {
  const createdSteps = renameReviewHelpers.getRenameApprovalSteps(sampleRequests[0]);
  assert.deepEqual(
    createdSteps.map((s) => s.state),
    ['current', 'pending', 'pending'],
  );

  const facultyPendingSteps = renameReviewHelpers.getRenameApprovalSteps(sampleRequests[1]);
  assert.deepEqual(
    facultyPendingSteps.map((s) => s.state),
    ['approved', 'current', 'pending'],
  );

  const rejectedAdvisorSteps = renameReviewHelpers.getRenameApprovalSteps(sampleRequests[2]);
  assert.deepEqual(
    rejectedAdvisorSteps.map((s) => s.state),
    ['rejected', 'pending', 'pending'],
  );

  const completedSteps = renameReviewHelpers.getRenameApprovalSteps(sampleRequests[3]);
  assert.deepEqual(
    completedSteps.map((s) => s.state),
    ['approved', 'approved', 'approved'],
  );
  assert.equal(completedSteps[2].timestamp, '2026-11-04T08:00:00Z');
});

test('RenameReviewPage renders Fluent PageHeader without icon, Admin NavigationTabs, StatisticsBar, DataTable with outline Xem button, and floating bar', () => {
  const states = [
    ['pending', null], // scope
    ['', null], // keyword
    ['', null], // batchId
    ['createdAt', null], // sortBy
    ['desc', null], // sortOrder
    [1, null], // page
    [10, null], // pageSize
    ['req-1', null], // selectedRowId
    [null, null], // openRequestId
    ['', null], // notice
  ];
  let hookIndex = 0;

  const mockReact = {
    ...React,
    useState: (initial) => {
      const idx = hookIndex++;
      if (!states[idx]) states[idx] = [typeof initial === 'function' ? initial() : initial, null];
      return [
        states[idx][0],
        (val) => {
          states[idx][0] = typeof val === 'function' ? val(states[idx][0]) : val;
        },
      ];
    },
    useMemo: (factory) => factory(),
    useEffect: () => {},
    useCallback: (fn) => fn,
  };

  let lastQueryFn;
  let lastMutationOpts;
  const fakeQueryClient = {
    invalidateQueries: async () => {},
  };

  const uiStub = (name) => name;
  const pageModule = loadModule('features/topics/pages/RenameReviewPage.tsx', {
    react: mockReact,
    'react/jsx-runtime': jsxRuntime,
    '@tanstack/react-query': {
      useQuery: (opts) => {
        lastQueryFn = opts.queryFn;
        return {
          data: sampleRequests,
          isPending: false,
          isFetching: false,
          isError: false,
          refetch: async () => {},
        };
      },
      useMutation: (opts) => {
        lastMutationOpts = opts;
        return { isPending: false, isError: false, error: null, reset() {}, mutate() {} };
      },
      useQueryClient: () => fakeQueryClient,
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
    '@/shared/auth': {
      useAuth: () => ({
        user: { id: 'admin-1', roles: ['Admin'] },
        hasRole: (r) => r === 'Admin',
      }),
    },
    '@/shared/notifications': { OperationToast: uiStub('OperationToast') },
    '../components/RenameReviewDialog': { RenameReviewDialog: 'RenameReviewDialog' },
    '../renameReview': renameReviewHelpers,
    '../services/renameRequestApi': {
      renameRequestApi: { list: async () => sampleRequests, review: async () => ({}) },
    },
    '../services/topicApi': { getTopicErrorMessage: (e) => String(e) },
    '../types/renameRequest.types': renameTypes,
  });

  const entry = pageModule.RenameReviewEntryPage();
  const tree = entry.type(entry.props);
  assert.equal(typeof lastQueryFn, 'function');
  assert.equal(typeof lastMutationOpts.mutationFn, 'function');

  // PageHeader must use accent, NOT pass a decorative icon
  const pageHeader = walk(tree, (n) => n.type === 'PageHeader')[0];
  assert.equal(pageHeader.props.accent, true);
  assert.equal(pageHeader.props.icon, undefined);

  // Admin NavigationTabs lives inside PageHeader navigation slot with per-level counts
  const navTabs = walk(pageHeader.props.navigation, (n) => n.type === 'NavigationTabs')[0];
  assert.equal(navTabs.props.tabs.length, 3);
  assert.deepEqual(
    navTabs.props.tabs.map((t) => [t.id, t.count]),
    [
      ['advisor', 1],
      ['faculty', 1],
      ['academic-affairs', 1],
    ],
  );

  // StatisticsBar inside ListCardHeader has 4 scopes without duplicating counts in description
  const statsBar = walk(tree, (n) => n.type === 'StatisticsBar')[0];
  assert.deepEqual(
    statsBar.props.items.map((item) => [item.key, item.value]),
    [
      ['pending', 1],
      ['completed', 1],
      ['rejected', 1],
      ['all', 5],
    ],
  );

  // Action button in both desktop table and mobile item uses outline "Xem" without Eye icon
  const dataTableNode = walk(tree, (n) => n.type === DataTable)[0];
  const tableTree = DataTable(dataTableNode.props);
  const viewButtons = walk(tableTree, (n) => n.type === 'Button' && n.props.children === 'Xem');
  assert.equal(viewButtons.length, 2);
  for (const btn of viewButtons) {
    assert.equal(btn.props.variant, 'outline');
    assert.equal(btn.props.leftIcon, undefined);
  }

  // TableFloatingBar appears for selected row req-1 with quick open action
  const floatingBar = walk(tree, (n) => n.type === 'TableFloatingBar')[0];
  assert.equal(floatingBar.props.selectedCount, 1);
});

test('RenameReviewDialog renders 2-column comparison, validates rejection reason, supports Alt+1/Alt+2 shortcuts, and opens ConfirmDialog', () => {
  const hookStates = [];
  let effectHandlers = [];
  let stateCursor = 0;

  const listeners = {};
  const fakeWindow = {
    addEventListener: (type, fn) => {
      listeners[type] = fn;
    },
    removeEventListener: (type, fn) => {
      if (listeners[type] === fn) delete listeners[type];
    },
  };

  const mockReact = {
    ...React,
    useState: (init) => {
      const idx = stateCursor++;
      if (!(idx in hookStates)) hookStates[idx] = typeof init === 'function' ? init() : init;
      return [
        hookStates[idx],
        (next) => {
          hookStates[idx] = typeof next === 'function' ? next(hookStates[idx]) : next;
        },
      ];
    },
    useCallback: (fn) => fn,
    useEffect: (fn) => {
      effectHandlers.push(fn);
    },
  };

  const { RenameReviewDialog } = loadModule(
    'features/topics/components/RenameReviewDialog.tsx',
    {
      react: mockReact,
      'react/jsx-runtime': jsxRuntime,
      '@tanstack/react-query': {
        useQuery: () => ({ data: null, isPending: false }),
      },
      'lucide-react': icons,
      '@/components/ui/avatar': { Avatar: 'Avatar' },
      '@/components/ui/button': { Button: 'Button' },
      '@/components/ui/confirm-dialog': { ConfirmDialog: 'ConfirmDialog' },
      '@/components/ui/dialog': { Dialog: 'Dialog' },
      '@/components/ui/inline-alert': { InlineAlert: 'InlineAlert' },
      '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
      '@/lib/utils': { cn: (...args) => args.filter(Boolean).join(' ') },
      '../services/studentTopicApi': { studentTopicApi: { getGroupDetail: async () => null } },
      '../renameReview': renameReviewHelpers,
      '../types/renameRequest.types': renameTypes,
    },
    fakeWindow,
  );

  const submitted = [];
  const renderDialog = (req = sampleRequests[4], lvl = 'academic-affairs') => {
    stateCursor = 0;
    effectHandlers = [];
    return RenameReviewDialog({
      request: req,
      levelConfig: renameReviewHelpers.RENAME_LEVEL_CONFIGS[lvl],
      busy: false,
      onClose() {},
      onSubmitDecision: (payload) => submitted.push(payload),
    });
  };

  // Level 3 dialog should show Academic Affairs notice and reject button requires reason first
  let tree = renderDialog(sampleRequests[4], 'academic-affairs');
  const textareas = walk(tree, (n) => n.type === 'textarea');
  assert.equal(textareas.length, 1);

  const rejectBtn = walk(
    tree,
    (n) => n.type === 'Button' && n.props.children === 'Từ chối có lý do',
  )[0];
  rejectBtn.props.onClick();

  // Re-render to observe validation alert without opening ConfirmDialog
  tree = renderDialog(sampleRequests[4], 'academic-affairs');
  let confirmDialog = walk(tree, (n) => n.type === 'ConfirmDialog')[0];
  assert.equal(confirmDialog.props.open, false);
  const errorAlert = walk(
    tree,
    (n) => n.type === 'p' && n.props.role === 'alert',
  )[0];
  assert.match(errorAlert.props.children, /Vui lòng nhập lý do từ chối/);

  // Attach keyboard listener and trigger Alt+1 to open Approve ConfirmDialog
  const cleanup = effectHandlers[0]();
  assert.equal(typeof listeners.keydown, 'function');
  listeners.keydown({
    altKey: true,
    key: '1',
    preventDefault() {},
  });

  tree = renderDialog(sampleRequests[4], 'academic-affairs');
  confirmDialog = walk(tree, (n) => n.type === 'ConfirmDialog')[0];
  assert.equal(confirmDialog.props.open, true);
  assert.equal(confirmDialog.props.confirmText, 'Phê duyệt cấp 3');

  confirmDialog.props.onConfirm();
  assert.equal(submitted.length, 1);
  assert.equal(submitted[0].decision, 'Approve');
  assert.equal(submitted[0].request.id, 'req-5');
  cleanup?.();
});

test('computeTitleWordDiff highlights replaced words cleanly and falls back for unrelated titles', () => {
  const diff = renameReviewHelpers.computeTitleWordDiff(
    '[TEST-GVPB] Hệ thống gợi ý đề tài KLTN bằng Semantic Search',
    '[TEST-GVPB] Hệ thống gợi ý đề tài khóa luận đa ngôn ngữ bằng Hybrid Search và LLM',
  );

  assert.equal(diff.hasDiff, true);
  assert.deepEqual(
    diff.oldSegments.filter((s) => s.kind === 'removed').map((s) => s.text),
    ['KLTN', 'Semantic'],
  );
  assert.deepEqual(
    diff.newSegments.filter((s) => s.kind === 'added').map((s) => s.text),
    ['khóa luận đa ngôn ngữ', 'Hybrid', 'và LLM'],
  );

  const unrelated = renameReviewHelpers.computeTitleWordDiff(
    'Ứng dụng Blockchain truy xuất nguồn gốc nông sản',
    'Phân tích cảm xúc bình luận mạng xã hội tiếng Việt',
  );
  assert.equal(unrelated.hasDiff, false);
  assert.equal(unrelated.oldSegments.length, 1);
  assert.equal(unrelated.oldSegments[0].kind, 'unchanged');
  assert.equal(unrelated.newSegments[0].kind, 'unchanged');
});
