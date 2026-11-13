import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { DataTable } from './helpers/data-table.mjs';

function load(path, dependencies = {}) {
  const source = readFileSync(new URL('../src/features/topics/' + path, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, 'Unexpected dependency: ' + name);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  return exports;
}
const success = (data) => ({ data: { success: true, data, error: null } });
const api = (get) =>
  load('services/advisingApi.ts', { '@/shared/api': { httpClient: { get } } }).advisingApi;
const helpers = load('advising.ts');
const types = load('types/advising.types.ts');
const base = { batchId: '', keyword: '', scope: 'active', status: '', confirmation: '' };

test('group list uses one paged group endpoint with server filters, global sorting and cancellation', async () => {
  const calls = [];
  const service = api(async (...args) => {
    calls.push(args);
    return success({ page: { items: [], totalCount: 42 }, statistics: { total: 42 } });
  });
  const signal = new AbortController().signal;
  const response = await service.getGroups(
    {
      ...base,
      scope: 'stopped',
      keyword: '  Nguyễn Đức  ',
      status: 'Stopped',
      pageNumber: 2,
      pageSize: 20,
      sortBy: 'title',
      sortOrder: 'asc',
    },
    signal,
  );
  assert.equal(response.page.totalCount, 42);
  assert.deepEqual(calls, [
    [
      '/api/thesis/advising/groups',
      {
        params: {
          ...base,
          batchId: undefined,
          scope: 'stopped',
          keyword: 'Nguyễn Đức',
          status: 'Stopped',
          confirmation: undefined,
          pageNumber: 2,
          pageSize: 20,
          sortBy: 'title',
          sortOrder: 'asc',
        },
        signal,
      },
    ],
  ]);
});

test('failed group requests remain errors and a group detail cannot switch identities', async () => {
  const failed = api(async () => ({
    data: { success: false, error: { message: 'Không đủ quyền' } },
  }));
  await assert.rejects(
    failed.getGroups({ ...base, pageNumber: 1, pageSize: 10, sortBy: 'title', sortOrder: 'asc' }),
    /Không đủ quyền/,
  );
  await assert.rejects(
    api(async () => success({ groupId: 'another' })).getGroupDetail('expected'),
    /không khớp nhóm/,
  );
  const calls = [];
  const signal = new AbortController().signal;
  const detail = await api(async (...args) => {
    calls.push(args);
    return success({ groupId: 'stopped-group', groupStatus: 'Stopped', members: [] });
  }).getGroupDetail('stopped-group', signal);
  assert.equal(detail.groupStatus, 'Stopped');
  assert.deepEqual(calls, [['/api/thesis/groups/stopped-group/detail', { signal }]]);
});

test('milestone compatibility still loads all scoped topic pages without accepting another advisor', async () => {
  const calls = [];
  const service = api(async (_path, config) => {
    calls.push(config.params);
    return success({
      items: [{ id: 'topic-' + config.params.pageNumber, advisorId: 'me' }],
      totalCount: 2,
      hasNextPage: config.params.pageNumber === 1,
    });
  });
  assert.equal((await service.getTopics('me', 'batch')).length, 2);
  assert.deepEqual(
    calls.map((call) => call.pageNumber),
    [1, 2],
  );
  await assert.rejects(
    api(async () =>
      success({
        items: [{ id: 'foreign', advisorId: 'other' }],
        totalCount: 1,
        hasNextPage: false,
      }),
    ).getTopics('me', ''),
    /không thuộc/,
  );
  await assert.rejects(service.getTopics('', ''), /Không xác định/);
});

test('every persisted group status has a Vietnamese label and group labels are stable identifiers', () => {
  const enumSource = readFileSync(
    new URL(
      '../../backend/Modules/ThesisManagement/Domain/Enums/StudentGroupStatus.cs',
      import.meta.url,
    ),
    'utf8',
  );
  const statuses = enumSource
    .slice(enumSource.indexOf('{') + 1, enumSource.lastIndexOf('}'))
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
  assert.deepEqual(Object.keys(types.GROUP_STATUS_LABELS).sort(), statuses.sort());
  assert.equal(types.GROUP_STATUS_LABELS.ReadyForReviewerAssignment, 'Chờ phân công phản biện');
  assert.equal(helpers.groupLabel('abcdef12-1234-5678-0000-000000000001'), 'Nhóm ABCDEF12');
});

test('confirmation labels use server-derived expiry rather than guessing from the client clock or joinedAt', () => {
  assert.equal(helpers.confirmationLabel('Confirmed'), 'Đã xác nhận');
  assert.equal(helpers.confirmationLabel('Pending', null), 'Đang gửi lời mời');
  assert.equal(helpers.confirmationLabel('Pending', '2020-01-01T00:00:00Z'), 'Chờ xác nhận');
  assert.equal(helpers.confirmationLabel('Expired', '2020-01-01T00:00:00Z'), 'Lời mời đã hết hạn');
  assert.equal(
    helpers.confirmationLabel('Pending', '2020-01-01T00:00:00Z', true),
    'Lời mời đã hết hạn',
  );
});

const groups = [
  {
    groupId: 'bbbbbbbb-0000',
    topicId: 'topic-b',
    titleVi: 'Beta',
    batchId: 'batch',
    batchName: 'Đợt thực tế',
    status: 'Registered',
    createdAt: '2026-10-01T00:00:00Z',
    confirmedStudentsCount: 1,
    pendingConfirmationsCount: 1,
    expiredInvitationsCount: 0,
    students: [],
  },
  {
    groupId: 'aaaaaaaa-0000',
    topicId: 'topic-a',
    titleVi: 'Alpha',
    batchId: 'batch',
    batchName: 'Đợt thực tế',
    status: 'Stopped',
    createdAt: '2026-10-01T00:00:00Z',
    confirmedStudentsCount: 1,
    pendingConfirmationsCount: 0,
    expiredInvitationsCount: 0,
    students: [],
  },
];
const statistics = {
  total: 42,
  active: 30,
  completed: 10,
  stopped: 2,
  activeConfirmedStudents: 35,
  pendingInvitations: 3,
  expiredInvitations: 1,
};
function fixture({
  initialFilters = base,
  queryState = {},
  userId = 'me',
  canReadBatches = true,
} = {}) {
  const state = [];
  let cursor = 0;
  let queries = [];
  const calls = [];
  const changes = [];
  const dependencies = {
    '@/components/ui/data-table': { DataTable },
    react: {
      ...React,
      useState(initial) {
        const index = cursor++;
        if (!(index in state)) state[index] = initial;
        return [
          state[index],
          (value) => {
            state[index] = typeof value === 'function' ? value(state[index]) : value;
          },
        ];
      },
    },
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@tanstack/react-query': {
      useQuery(options) {
        queries.push(options);
        return {
          data: {
            page: {
              items: groups,
              totalCount: 42,
              pageNumber: options.queryKey[3],
              pageSize: options.queryKey[4],
            },
            statistics,
          },
          isPending: false,
          isError: false,
          isFetching: false,
          refetch() {
            calls.push('retry');
          },
          ...queryState,
        };
      },
    },
    '@/shared/auth': {
      useAuth: () => ({
        user: userId ? { id: userId } : null,
        hasPermission: () => canReadBatches,
      }),
    },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({
        data: [{ id: 'batch', batchName: 'Đợt thực tế' }],
        isPending: false,
        isError: false,
      }),
    },
    '../services/advisingApi': {
      advisingApi: {
        getGroups: async (...args) => {
          calls.push(args);
          return { page: { items: groups }, statistics };
        },
      },
    },
    '../services/topicApi': {
      getTopicErrorMessage: (error) => error?.message ?? 'Không tải được nhóm',
    },
    '../types/advising.types': types,
    '../advising': helpers,
    '../components/AdvisingGroupDetail': {
      AdvisingGroupDetail: 'AdvisingGroupDetail',
      GroupStatusBadge: 'GroupStatusBadge',
    },
    '@/components/ui/table': Object.fromEntries(
      [
        'Table',
        'TableHeader',
        'TableHead',
        'TableBody',
        'TableCell',
        'TableEmpty',
        'TableActiveFilters',
      ].map((n) => [n, n]),
    ),
  };
  for (const [path, name] of Object.entries({
    button: 'Button',
    'avatar-group': 'AvatarGroup',
    input: 'Input',
    select: 'Select',
    'statistics-bar': 'StatisticsBar',
    'selectable-table-row': 'SelectableTableRow',
    'selection-indicator': 'SelectionIndicator',
    'table-pagination': 'TablePagination',
    'table-skeleton': 'TableSkeleton',
  }))
    dependencies['@/components/ui/' + path] = { [name]: name };
  const { TopicAdvisingPage } = load('pages/TopicAdvisingPage.tsx', dependencies);
  return {
    render() {
      cursor = 0;
      queries = [];
      return TopicAdvisingPage({
        initialFilters,
        onFiltersChange: (filters) => changes.push(filters),
      });
    },
    queries: () => queries,
    calls,
    changes,
  };
}
function nodes(tree) {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  if (tree?.type === DataTable) return nodes(DataTable(tree.props));
  return React.isValidElement(tree) ? [tree, ...nodes(tree.props.children)] : [];
}
function text(tree) {
  if (Array.isArray(tree)) return tree.map(text).join(' ');
  if (React.isValidElement(tree)) return text(tree.props.children);
  return typeof tree === 'string' || typeof tree === 'number' ? String(tree) : '';
}
function find(tree, predicate) {
  const found = nodes(tree).find(predicate);
  assert.ok(found, 'Expected UI element');
  return found;
}
const component = (tree, type) => find(tree, (node) => node.type === type);
const button = (tree, label) =>
  find(tree, (node) => node.type === 'Button' && text(node) === label);

test('guidance page consumes server totals and exposes no duplicated tab list or client pagination', async () => {
  const ui = fixture();
  const tree = ui.render();
  const query = ui.queries()[0];
  assert.equal(ui.queries().length, 1);
  assert.equal(query.enabled, true);
  assert.equal(query.refetchInterval, 60000);
  const signal = new AbortController().signal;
  await query.queryFn({ signal });
  assert.deepEqual(ui.calls[0], [
    { ...base, pageNumber: 1, pageSize: 10, sortBy: 'createdAt', sortOrder: 'desc' },
    signal,
  ]);
  assert.equal(component(tree, 'TablePagination').props.totalCount, 42);
  assert.equal(
    component(tree, 'StatisticsBar').props.items.find((item) => item.key === 'stopped').value,
    '2',
  );
  assert.equal(
    nodes(tree).filter((node) => node.type === 'StatusTabs' || node.type === 'NavigationTabs')
      .length,
    0,
  );
  assert.equal(nodes(tree).filter((node) => node.type === 'SelectableTableRow').length, 2);
});

test('sort requests reset to page one and leave sorting of the full dataset to the server', () => {
  const ui = fixture();
  let tree = ui.render();
  component(tree, 'TablePagination').props.onPageChange(3);
  tree = ui.render();
  find(tree, (node) => node.type === 'TableHead' && text(node) === 'Đề tài & nhóm').props.onSort();
  tree = ui.render();
  const query = ui.queries()[0];
  assert.equal(query.queryKey[3], 1);
  assert.equal(query.queryKey[5], 'title');
  assert.equal(query.queryKey[6], 'asc');
  // The server page is authoritative: the FE must not reshuffle it independently.
  const rows = nodes(tree).filter((node) => node.type === 'SelectableTableRow');
  assert.deepEqual(
    rows.map((row) => row.key),
    groups.map((group) => group.groupId),
  );
});

test('filter edits wait for apply and chip removal retains the other applied filters', () => {
  const ui = fixture();
  let tree = ui.render();
  component(tree, 'Input').props.onChange({ target: { value: '  nguyen  ' } });
  tree = ui.render();
  assert.equal(ui.queries()[0].queryKey[2].keyword, '');
  find(tree, (node) => node.props['aria-label'] === 'Đợt khóa luận').props.onChange('batch');
  tree = ui.render();
  component(tree, 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.equal(ui.queries()[0].queryKey[2].keyword, 'nguyen');
  assert.equal(ui.queries()[0].queryKey[2].batchId, 'batch');
  component(tree, 'TableActiveFilters')
    .props.chips.find((chip) => chip.key === 'keyword')
    .onRemove();
  ui.render();
  assert.equal(ui.queries()[0].queryKey[2].keyword, '');
  assert.equal(ui.queries()[0].queryKey[2].batchId, 'batch');
  assert.deepEqual(ui.changes.at(-1), { ...base, batchId: 'batch' });
});

test('history and pending quick filters select disjoint scopes with truthful labels', () => {
  const ui = fixture();
  let tree = ui.render();
  component(tree, 'StatisticsBar')
    .props.items.find((item) => item.key === 'stopped')
    .onClick();
  tree = ui.render();
  assert.equal(ui.queries()[0].queryKey[2].scope, 'stopped');
  component(tree, 'StatisticsBar')
    .props.items.find((item) => item.key === 'pending')
    .onClick();
  ui.render();
  assert.deepEqual(ui.queries()[0].queryKey[2], {
    ...base,
    scope: 'active',
    confirmation: 'pending',
  });
});

test('list failures hide cached rows/statistics and allow retry; loading disables row actions', () => {
  const ui = fixture({ queryState: { isError: true, error: new Error('Máy chủ lỗi') } });
  const tree = ui.render();
  assert.equal(nodes(tree).filter((node) => node.type === 'SelectableTableRow').length, 0);
  assert.ok(component(tree, 'StatisticsBar').props.items.every((item) => item.value === '—'));
  button(tree, 'Thử lại').props.onClick();
  assert.deepEqual(ui.calls, ['retry']);
  const refreshing = fixture({ queryState: { isFetching: true } }).render();
  assert.ok(
    nodes(refreshing)
      .filter((node) => node.type === 'SelectableTableRow')
      .every((node) => node.props.disabled),
  );
});

test('details open by group ID so a historical group cannot silently become the current group', () => {
  const ui = fixture();
  let tree = ui.render();
  find(
    tree,
    (node) => node.type === 'Button' && node.props['aria-label']?.startsWith('Xem Nhóm AAAAAAAA'),
  ).props.onClick();
  tree = ui.render();
  assert.equal(component(tree, 'AdvisingGroupDetail').props.groupId, groups[1].groupId);
});

test('missing batch permission does not block reading own groups and missing identity prevents the request', () => {
  const noBatches = fixture({ canReadBatches: false });
  const tree = noBatches.render();
  assert.equal(
    find(tree, (node) => node.props['aria-label'] === 'Đợt khóa luận').props.disabled,
    true,
  );
  assert.equal(noBatches.queries()[0].enabled, true);
  const noUser = fixture({ userId: null });
  noUser.render();
  assert.equal(noUser.queries()[0].enabled, false);
});
