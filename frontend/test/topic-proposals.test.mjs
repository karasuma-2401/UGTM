import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import axios from 'axios';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';
import { DataTable } from './helpers/data-table.mjs';

function loadModule(path, dependencies = {}, browserWindow = {}) {
  dependencies = {
    '@/shared/notifications': { OperationToast: 'OperationToast', notify: { success() {} } },
    '@/components/ui/inline-alert': { InlineAlert: 'InlineAlert' },
    ...dependencies,
  };
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function(
    'require',
    'exports',
    'window',
    'localStorage',
    'document',
    'HTMLElement',
    outputText,
  )(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    browserWindow,
    browserWindow.localStorage ?? { getItem: () => null, setItem() {}, removeItem() {} },
    browserWindow.document ?? {},
    browserWindow.HTMLElement ?? class {},
  );
  return exports;
}

const success = (data) => ({ data: { success: true, data, error: null } });
const values = {
  batchId: 'batch-1',
  titleVi: '  Đề tài thử nghiệm  ',
  titleEn: '  ',
  description: '  Mục tiêu  ',
  requirements: '',
  studentNumber: 2,
};

function apiFixture(client) {
  return loadModule('features/topics/services/topicApi.ts', {
    axios: { default: axios },
    '@/shared/api': { httpClient: client },
    '../proposalEditing': loadModule('features/topics/proposalEditing.ts'),
  });
}

test('creating a proposal uses POST with the batch, normalized content and draft/submit choice', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    post: async (...args) => {
      calls.push(args);
      return success({ id: 'created', status: args[1].submitImmediately ? 'Submitted' : 'Draft' });
    },
  });
  assert.equal((await topicApi.save(null, values, false)).status, 'Draft');
  assert.equal((await topicApi.save(null, values, true)).status, 'Submitted');
  assert.deepEqual(calls[0], [
    '/api/topics',
    {
      batchId: 'batch-1',
      titleVi: 'Đề tài thử nghiệm',
      titleEn: null,
      description: 'Mục tiêu',
      requirements: null,
      studentNumber: 2,
      submitImmediately: false,
    },
  ]);
  assert.equal(calls[1][1].submitImmediately, true);
});

test('editing and resubmitting uses one PUT without changing the batch or advisor', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    put: async (...args) => {
      calls.push(args);
      return success({ id: 'topic-1' });
    },
  });
  await topicApi.save('topic-1', values, true);
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], '/api/topics/topic-1');
  assert.equal(calls[0][1].submitImmediately, true);
  assert.equal('batchId' in calls[0][1], false);
  assert.equal('advisorId' in calls[0][1], false);
});

test('an unchanged saved proposal uses the submit endpoint without a request body', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    post: async (...args) => {
      calls.push(args);
      return success({ status: 'Submitted' });
    },
  });
  assert.equal((await topicApi.submit('topic-1')).status, 'Submitted');
  assert.deepEqual(calls, [['/api/topics/topic-1/submit']]);
});

test('creating a proposal assigns trimmed MSSV through the existing create request', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    post: async (...args) => {
      calls.push(args);
      return success({ id: 'created' });
    },
  });
  await topicApi.save(null, { ...values, studentCodes: [' 24521603 ', ' sv02 '] }, false);
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], '/api/topics');
  assert.deepEqual(calls[0][1].studentCodes, ['24521603', 'SV02']);
  assert.equal('clearAssignedStudents' in calls[0][1], false);
});

test('assignment edits replace explicitly; content edits never resend or clear the roster', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    put: async (...args) => {
      calls.push(args);
      return success({ id: 'topic-1' });
    },
  });
  const assigned = { ...values, studentCodes: ['24521603'] };
  await topicApi.save('topic-1', assigned, false);
  assert.equal('studentCodes' in calls[0][1], false);
  assert.equal('clearAssignedStudents' in calls[0][1], false);
  await topicApi.save('topic-1', assigned, false, true);
  assert.deepEqual(calls[1][1].studentCodes, ['24521603']);
  await topicApi.save('topic-1', { ...values, studentCodes: [] }, false, true);
  assert.equal(calls[2][1].clearAssignedStudents, true);
  assert.equal('studentCodes' in calls[2][1], false);
});

test('duplicate and over-capacity assignments never reach the backend', async () => {
  let posts = 0;
  const { topicApi } = apiFixture({
    post: async () => {
      posts++;
      return success({});
    },
  });
  await assert.rejects(
    topicApi.save(null, { ...values, studentCodes: ['SV01', ' sv01 '] }, false),
    /trùng/,
  );
  await assert.rejects(
    topicApi.save(null, { ...values, studentNumber: 1, studentCodes: ['SV01', 'SV02'] }, false),
    /chỉ tiêu/,
  );
  assert.equal(posts, 0);
});

test('an assignment failure preserves the backend message and leaves input intact', async () => {
  const input = { ...values, studentCodes: ['24521603'] };
  const { topicApi } = apiFixture({
    post: async () => ({
      data: { success: false, error: { message: 'Đã hết giai đoạn chuẩn bị đề tài.' } },
    }),
  });
  await assert.rejects(topicApi.save(null, input, false), /Đã hết giai đoạn chuẩn bị/);
  assert.deepEqual(input.studentCodes, ['24521603']);
});

test('topic list scopes privileged lecturer accounts to their own advisor ID and forwards paging/cancellation', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (...args) => {
      calls.push(args);
      return success({ items: [], totalCount: 0 });
    },
  });
  const query = {
    advisorId: 'lecturer-1',
    pageNumber: 2,
    pageSize: 10,
    keyword: '  học sâu  ',
    batchId: 'batch-1',
    status: 'Draft',
  };
  const signal = new AbortController().signal;
  await topicApi.getMyTopics(query, signal);
  assert.deepEqual(calls, [
    ['/api/topics/my-topics', { params: { ...query, keyword: 'học sâu' }, signal }],
  ]);
  await topicApi.getDetail('topic-1', signal);
  assert.deepEqual(calls[1], ['/api/topics/topic-1/tracking', { signal }]);
});

test('statistics use one dedicated endpoint scoped by the server identity and batch', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (path, options) => {
      calls.push([path, options]);
      return success({ total: 84, submitted: 15, needRevision: 7 });
    },
  });
  const signal = new AbortController().signal;
  assert.deepEqual(await topicApi.getStatistics('admin-1', 'batch-1', signal), {
    total: 84,
    submitted: 15,
    needRevision: 7,
  });
  assert.deepEqual(calls, [
    ['/api/topics/my-statistics', { params: { batchId: 'batch-1' }, signal }],
  ]);
});

test('empty filters are omitted and failed statistics do not silently display zero', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (_path, options) => {
      calls.push(options.params);
      if (_path === '/api/topics/my-statistics') throw new Error('Offline');
      return success({ items: [], totalCount: 0 });
    },
  });
  await topicApi.getMyTopics({
    advisorId: 'lecturer-1',
    batchId: '',
    keyword: '  ',
    status: '',
    pageNumber: 1,
    pageSize: 20,
  });
  assert.equal(calls[0].batchId, undefined);
  assert.equal(calls[0].keyword, undefined);
  assert.equal(calls[0].status, undefined);
  await assert.rejects(topicApi.getStatistics('lecturer-1'), /Offline/);
});

test('API failures preserve backend validation messages rather than reporting success', async () => {
  const { topicApi, getTopicErrorMessage } = apiFixture({
    post: async () => ({
      data: {
        success: false,
        data: null,
        error: { code: 'Topic.DuplicateTitle', message: 'Đề tài đã tồn tại.' },
      },
    }),
  });
  await assert.rejects(topicApi.save(null, values, false), /Đề tài đã tồn tại/);
  const error = new axios.AxiosError('Request failed', undefined, undefined, undefined, {
    status: 409,
    data: { success: false, error: { message: 'Trạng thái đề tài đã thay đổi.' } },
  });
  assert.equal(getTopicErrorMessage(error), 'Trạng thái đề tài đã thay đổi.');
});

test('statistics quick filters are disabled during loading and expose selected state', () => {
  const cn = (...parts) => parts.filter(Boolean).join(' ');
  const { Skeleton } = loadModule('components/ui/skeleton.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn },
  });
  const { SELECTION_TONES } = loadModule('components/ui/selection-tones.ts', {});
  const { StatisticsBar } = loadModule('components/ui/statistics-bar.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn },
    './selection-indicator': { SelectionIndicator: 'SelectionIndicator' },
    './selection-tones': { SELECTION_TONES },
    './skeleton': { Skeleton },
  });
  const items = [
    {
      key: 'total',
      label: 'Tổng đề tài',
      value: 12345,
      onClick() {},
      active: true,
      icon: React.createElement('svg', { 'data-ready-icon': true }),
    },
  ];
  const loading = renderToStaticMarkup(
    React.createElement(StatisticsBar, { items, loading: true }),
  );
  assert.match(loading, /Tổng đề tài/);
  assert.match(loading, /disabled=""/);
  assert.match(loading, /aria-pressed="false"/);
  assert.match(loading, /animate-pulse/);
  assert.equal((loading.match(/animate-pulse/g) ?? []).length, 3);
  assert.match(loading, /aria-busy="true"/);
  assert.match(loading, /class="invisible" aria-hidden="true"/);
  assert.doesNotMatch(loading, /data-ready-icon|ring-primary\/40/);
  assert.doesNotMatch(loading, /12345/);
  const ready = renderToStaticMarkup(React.createElement(StatisticsBar, { items }));
  assert.match(ready, /12345/);
  assert.match(ready, /aria-pressed="true"/);
  assert.match(ready, /data-ready-icon/);
  assert.doesNotMatch(ready, /animate-pulse/);
  assert.doesNotMatch(ready, /disabled=""/);
});

test('table loading matches topic cells, checkboxes, badges and action buttons', () => {
  const cn = (...parts) => parts.filter(Boolean).join(' ');
  const { Skeleton } = loadModule('components/ui/skeleton.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn },
  });
  const { TableSkeleton } = loadModule('components/ui/table-skeleton.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn },
    './skeleton': { Skeleton },
  });
  const markup = renderToStaticMarkup(
    React.createElement(TableSkeleton, {
      rows: 2,
      columns: [
        { label: 'STT', width: 'w-14', variant: 'number' },
        { label: '', width: 'w-12', variant: 'checkbox' },
        { label: 'Đề tài', variant: 'topic' },
        { label: 'Trạng thái', variant: 'badge' },
        { label: 'Thao tác', variant: 'actions', actionCount: 2 },
      ],
    }),
  );
  assert.match(markup, /role="status" aria-busy="true"/);
  assert.equal((markup.match(/h-\[88px\]/g) ?? []).length, 2);
  assert.equal((markup.match(/size-9 shrink-0/g) ?? []).length, 2);
  assert.equal((markup.match(/h-9 w-24/g) ?? []).length, 4);
  assert.equal((markup.match(/size-4 rounded-sm/g) ?? []).length, 3);
  assert.match(markup, /Đề tài/);
  assert.doesNotMatch(markup, /<button|<input/);
});

const { navigationSections } = loadModule('shared/layout/navigationConfig.ts', {
  'lucide-react': icons,
});

function renderSidebar(roles) {
  const Link = ({ to, children, ...props }) =>
    React.createElement('a', { ...props, href: to }, children);
  const { SidebarNavigation } = loadModule('shared/layout/SidebarNavigation.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': { NavLink: ({ end: _end, ...props }) => React.createElement(Link, props) },
    'lucide-react': icons,
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    '@/components/ui/badge': {
      Badge: ({ children }) => React.createElement('span', null, children),
    },
  });
  const { AppLayout } = loadModule('shared/layout/AppLayout.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': {
      Link,
      NavLink: Link,
      Outlet: () => null,
      useLocation: () => ({ pathname: '/topics/advising' }),
    },
    '../../../assets/logo_UGTM.svg': { default: '/logo.svg' },
    'lucide-react': icons,
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    '@/components/ui/badge': {
      Badge: ({ children }) => React.createElement('span', null, children),
    },
    '@/components/ui/button': {
      Button: ({ children }) => React.createElement('button', null, children),
    },
    '@/components/ui/input': { Input: () => React.createElement('input') },
    '@/shared/auth': {
      useAuth: () => ({
        user: { fullName: 'Giảng viên thử nghiệm', roles },
        hasRole: (required) =>
          (Array.isArray(required) ? required : [required]).some((role) =>
            roles.some((actual) => actual.toLowerCase() === role.toLowerCase()),
          ),
        hasPermission: () => false,
        logout: async () => {},
        isLoggingOut: false,
      }),
    },
    '@/shared/auth/identityAccess': { getIdentityAccess: () => ({ canAccess: false }) },
    './navigationConfig': { navigationSections },
    './CurrentAcademicBatch': { CurrentAcademicBatch: () => null },
    './SidebarNavigation': { SidebarNavigation },
  });
  return renderToStaticMarkup(React.createElement(AppLayout));
}

test('actual AppLayout renders one guidance entry for Lecturer and Admin, including mixed roles', () => {
  for (const roles of [
    ['Lecturer'],
    ['lecturer'],
    ['Admin'],
    ['Faculty', 'Lecturer'],
    ['Admin', 'Lecturer'],
  ]) {
    const markup = renderSidebar(roles);
    const sidebarMarkup = markup.match(/<aside[\s\S]*?<\/aside>/)?.[0] ?? '';
    assert.match(sidebarMarkup, /href="\/topics\/advising"/);
    assert.equal((sidebarMarkup.match(/href="\/topics\/advising"/g) ?? []).length, 1);
    assert.doesNotMatch(markup, /href="\/topics\/propose"/);
    assert.match(markup, /Đề tài hướng dẫn/);
    assert.doesNotMatch(markup, /Không có phân hệ nào khả dụng/);
  }
});

test('actual AppLayout hides guidance from accounts without Lecturer or Admin', () => {
  for (const roles of [['Student'], ['Faculty'], ['CouncilMember'], []]) {
    assert.doesNotMatch(renderSidebar(roles), /href="\/topics\/advising"/);
  }
});

test('actual sidebar renders one catalog link for Student/Admin and hides it from other roles', () => {
  for (const roles of [['Student'], ['student'], ['Admin'], ['admin'], ['Admin', 'Student']]) {
    const markup = renderSidebar(roles);
    assert.equal((markup.match(/href="\/topics\/catalog"/g) ?? []).length, 1);
    assert.match(markup, /Đề tài &amp; đăng ký/);
  }
  for (const roles of [['Lecturer'], ['Faculty'], ['AcademicAffairs'], []]) {
    assert.doesNotMatch(renderSidebar(roles), /href="\/topics\/catalog"/);
  }
});

test('review sidebar entry is visible only to Faculty and Admin, including mixed roles', () => {
  for (const roles of [['Faculty'], ['faculty'], ['Admin'], ['Faculty', 'Lecturer']]) {
    assert.match(renderSidebar(roles), /href="\/topics\/review"/);
  }
  for (const roles of [['Lecturer'], ['Student'], ['CouncilMember'], ['AcademicAffairs'], []]) {
    assert.doesNotMatch(renderSidebar(roles), /href="\/topics\/review"/);
  }
});

test('only Draft and NeedRevision permit editing', () => {
  const { canEditTopic } = loadModule('features/topics/types/topic.types.ts');
  for (const status of ['Draft', 'NeedRevision']) assert.equal(canEditTopic(status), true);
  for (const status of ['Submitted', 'Approved', 'Rejected', 'Published', 'Closed', 'Archived']) {
    assert.equal(canEditTopic(status), false);
  }
});

test('guidance has two tabs, preserves batch scope and resolves legacy group bookmarks', () => {
  function render(search = '') {
    const { TopicGuidancePage } = loadModule('features/topics/pages/TopicGuidancePage.tsx', {
      'lucide-react': icons,
      'react/jsx-runtime': jsxRuntime,
      'react-router-dom': { useSearchParams: () => [new URLSearchParams(search), () => {}] },
      '@/components/ui/navigation-tabs': { NavigationTabs: 'NavigationTabs' },
      './TopicProposePage': { TopicProposePage: 'TopicProposePage' },
      './TopicAdvisingPage': { TopicAdvisingPage: 'TopicAdvisingPage' },
      '@/components/ui/page-header': { PageHeader: 'PageHeader' },
      '../types/advising.types': loadModule('features/topics/types/advising.types.ts'),
      '../types/topic.types': loadModule('features/topics/types/topic.types.ts'),
    });
    return TopicGuidancePage();
  }
  const page = render('batchId=batch-real');
  assert.equal(page.type, 'TopicProposePage');
  assert.equal(page.props.initialFilters.batchId, 'batch-real');
  assert.deepEqual(
    page.props.navigation.props.tabs.map((tab) => [tab.label, tab.href]),
    [
      ['Đề tài của tôi', '/topics/advising?batchId=batch-real'],
      ['Nhóm & tiến độ', '/topics/advising?tab=groups&batchId=batch-real'],
    ],
  );
  for (const tab of ['registrations', 'advising', 'groups']) {
    const groups = render(`tab=${tab}&batchId=batch-real&status=ReadyForReviewerAssignment`);
    const pane = findElement(groups, (node) => node.type === 'TopicAdvisingPage');
    assert.equal(pane.props.initialFilters.batchId, 'batch-real');
    assert.equal(pane.props.initialFilters.status, 'ReadyForReviewerAssignment');
    assert.equal(pane.props.initialFilters.scope, tab === 'registrations' ? 'all' : 'active');
    assert.equal(
      findElement(groups, (node) => node.type === 'PageHeader').props.navigation.props.tabs.length,
      2,
    );
  }
  assert.equal(render('tab=invalid').type, 'TopicProposePage');
});

function findElement(node, predicate) {
  if (!React.isValidElement(node)) return undefined;
  if (predicate(node)) return node;
  for (const child of React.Children.toArray(node.props.children)) {
    const found = findElement(child, predicate);
    if (found) return found;
  }
}

// Keep React/router/query boundaries controlled while executing the actual page
// event handlers. No real proposals are created by these interaction tests.
function proposalPageFixture(options = {}) {
  const state = [];
  let cursor = 0;
  let tree;
  let listQuery;
  let listQueryOptions;
  let beforeUnload;
  let shouldBlock;
  let blockerState = 'unblocked';
  let proceeds = 0;
  let mutationCalls = 0;
  const mutationVariables = [];
  const effects = [];
  let effectCursor = 0;
  let mutationCursor = 0;
  const mutations = [];
  const pendingJobs = [];
  const invalidations = [];
  const apiCalls = [];
  const timers = new Map();
  const frames = new Map();
  let nextTask = 1;
  const saved = options.saved ?? new Map();
  const focused = [];
  class Element {
    constructor(id) {
      this.id = id;
    }
    matches() {
      return !this.id.endsWith('-field');
    }
    querySelector() {
      return new Element(`${this.id}-button`);
    }
    focus() {
      focused.push(this.id);
    }
    scrollIntoView() {}
  }
  const browserWindow = {
    confirm: () => true,
    setTimeout: (callback) => {
      const id = nextTask++;
      timers.set(id, callback);
      return id;
    },
    clearTimeout: (id) => timers.delete(id),
    requestAnimationFrame: (callback) => {
      const id = nextTask++;
      frames.set(id, callback);
      return id;
    },
    cancelAnimationFrame: (id) => frames.delete(id),
    localStorage: {
      getItem: (key) => saved.get(key) ?? null,
      setItem: (key, value) => {
        if (options.failLocalWrite) throw new Error('Storage full');
        saved.set(key, value);
      },
      removeItem: (key) => saved.delete(key),
    },
    document: { getElementById: (id) => new Element(id) },
    HTMLElement: Element,
  };
  const editing = loadModule('features/topics/proposalEditing.ts', {}, browserWindow);
  const componentNames = [
    'Button',
    'Input',
    'Select',
    'Dialog',
    'ConfirmDialog',
    'StatisticsBar',
    'Card',
    'CardContent',
    'CardHeader',
    'CardTitle',
    'TopicStatusBadge',
    'TopicsTable',
    'TopicPreview',
    'TopicReviewNote',
    'TopicStudentAssignmentField',
    'PageHeader',
    'ListCardHeader',
    'TopicProposalSchedule',
    'TablePagination',
    'TableSkeleton',
    'Skeleton',
    'TableActiveFilters',
    'TableFloatingBar',
  ];
  const components = Object.fromEntries(componentNames.map((name) => [name, () => null]));
  const dependency = (name) => ({ [name]: components[name] });
  const { TopicProposePage } = loadModule(
    'features/topics/pages/TopicProposePage.tsx',
    {
      react: {
        useCallback: (callback) => callback,
        useEffect: (callback, deps) => {
          const index = effectCursor++;
          const old = effects[index];
          if (!old || deps.some((dep, position) => !Object.is(dep, old.deps[position]))) {
            old?.cleanup?.();
            effects[index] = { deps, callback, pending: true };
          }
        },
        useState: (initial) => {
          const index = cursor++;
          if (!(index in state)) state[index] = typeof initial === 'function' ? initial() : initial;
          return [
            state[index],
            (value) => {
              state[index] = typeof value === 'function' ? value(state[index]) : value;
            },
          ];
        },
      },
      'react/jsx-runtime': jsxRuntime,
      'react-router-dom': {
        useBeforeUnload: (callback) => {
          beforeUnload = callback;
        },
        useBlocker: (condition) => {
          shouldBlock = condition;
          return {
            state: blockerState,
            reset: () => {
              blockerState = 'unblocked';
            },
            proceed: () => {
              proceeds++;
            },
          };
        },
      },
      '@tanstack/react-query': {
        useQueryClient: () => ({
          invalidateQueries: async ({ queryKey }) => {
            invalidations.push(queryKey);
          },
        }),
        useQuery: (queryOptions) => {
          if (typeof queryOptions.queryKey[2] === 'object') {
            listQuery = queryOptions.queryKey;
            listQueryOptions = queryOptions;
          }
          return {
            data:
              typeof queryOptions.queryKey[2] === 'object'
                ? {
                    items: options.listedTopic ? [options.listedTopic] : [],
                    pageNumber: queryOptions.queryKey[3],
                    pageSize: queryOptions.queryKey[4],
                    totalCount: 30,
                  }
                : { total: 30, submitted: 4, needRevision: 2 },
            isPending: false,
            isError: false,
            isFetching: false,
            ...options.queryState,
          };
        },
        useMutation: (configuration) => {
          const index = mutationCursor++;
          mutations[index] = configuration;
          return {
            mutate: (variables) => {
              mutationCalls++;
              mutationVariables.push(variables);
              if (index === 0)
                pendingJobs.push(
                  Promise.resolve(configuration.mutationFn(variables)).then((topic) =>
                    configuration.onSuccess(topic, variables),
                  ),
                );
            },
            reset() {},
            isPending: false,
            isError: false,
          };
        },
      },
      'lucide-react': icons,
      '@/components/ui/button': dependency('Button'),
      '@/components/ui/input': dependency('Input'),
      '@/components/ui/select': dependency('Select'),
      '@/components/ui/dialog': dependency('Dialog'),
      '@/components/ui/confirm-dialog': dependency('ConfirmDialog'),
      '@/components/ui/statistics-bar': dependency('StatisticsBar'),
      '@/components/ui/status-tabs': dependency('StatusTabs'),
      '@/components/ui/list-card-header': dependency('ListCardHeader'),
      '@/components/ui/card': Object.fromEntries(
        ['Card', 'CardContent', 'CardHeader', 'CardTitle'].map((key) => [key, components[key]]),
      ),
      '@/components/ui/table-pagination': dependency('TablePagination'),
      '@/components/ui/table-skeleton': dependency('TableSkeleton'),
      '@/components/ui/table': dependency('TableActiveFilters'),
      '@/components/ui/table-floating-bar': dependency('TableFloatingBar'),
      '@/components/ui/skeleton': dependency('Skeleton'),
      '../components/TopicStatusBadge': dependency('TopicStatusBadge'),
      '../components/TopicsTable': dependency('TopicsTable'),
      '../components/TopicPreview': dependency('TopicPreview'),
      '../components/TopicReviewNote': dependency('TopicReviewNote'),
      '../components/TopicStudentAssignmentField': dependency('TopicStudentAssignmentField'),
      '@/components/ui/page-header': dependency('PageHeader'),
      '../components/TopicProposalSchedule': dependency('TopicProposalSchedule'),
      '../proposalEditing': editing,
      '@/shared/auth': {
        useAuth: () => ({
          user: { id: 'lecturer-1', fullName: 'Giảng viên' },
          hasPermission: () => options.canReadBatches !== false,
        }),
      },
      '@/shared/academic-periods/useAcademicBatches': {
        useAcademicBatches: () => ({ data: [], isPending: false, isError: false }),
      },
      '../types/topic.types': loadModule('features/topics/types/topic.types.ts'),
      '../services/topicApi': {
        topicApi: {
          getDetail: async () => options.serverTopic ?? options.listedTopic,
          save: async (...args) => {
            apiCalls.push(['save', ...args]);
          },
          submit: async (...args) => {
            apiCalls.push(['submit', ...args]);
          },
          lookupStudentByCode: async (...args) => {
            apiCalls.push(['lookupStudentByCode', ...args]);
            return null;
          },
        },
        getTopicErrorMessage: String,
      },
    },
    browserWindow,
  );
  const fixture = {
    render() {
      cursor = 0;
      effectCursor = 0;
      mutationCursor = 0;
      tree = TopicProposePage(options.pageProps);
      effects.forEach((effect) => {
        if (effect.pending) {
          effect.pending = false;
          effect.cleanup = effect.callback();
        }
      });
      return tree;
    },
    find: (predicate) => findElement(tree, predicate),
    button: (label) =>
      findElement(tree, (node) => node.type === components.Button && node.props.children === label),
    component: (name) => findElement(tree, (node) => node.type === components[name]),
    confirm: () =>
      findElement(tree, (node) => node.type === components.ConfirmDialog && node.props.open),
    query: () => listQuery,
    listOptions: () => listQueryOptions,
    unload: (event) => beforeUnload(event),
    shouldBlock: () => shouldBlock,
    block: () => {
      blockerState = 'blocked';
    },
    proceeds: () => proceeds,
    mutationCalls: () => mutationCalls,
    invalidations,
    apiCalls,
    executeSave: () =>
      mutations[1].mutationFn(mutationVariables.findLast((value) => 'submitImmediately' in value)),
    mutationVariables: () => mutationVariables,
    saveCalls: () => mutationVariables.filter((value) => 'submitImmediately' in value),
    settle: async () => {
      await Promise.all(pendingJobs.splice(0));
    },
    completeSave: async (topic) =>
      mutations[1].onSuccess(
        topic,
        mutationVariables.findLast((value) => 'submitImmediately' in value),
      ),
    flushTimers: () => {
      for (const [id, callback] of [...timers]) {
        timers.delete(id);
        callback();
      }
    },
    flushFrames: () => {
      for (const [id, callback] of [...frames]) {
        frames.delete(id);
        callback();
      }
    },
    focused,
    saved,
    editing,
  };
  fixture.render();
  return fixture;
}

test('proposal refresh retains only the same lecturer data and locks table actions', () => {
  const page = proposalPageFixture({
    listedTopic: fullTopic,
    queryState: { isFetching: true, isPending: false },
  });
  const query = page.listOptions();
  const previous = { items: [fullTopic] };
  assert.equal(query.placeholderData(previous, { queryKey: query.queryKey }), previous);
  assert.equal(
    query.placeholderData(previous, { queryKey: ['my-proposed-topics', 'another-lecturer'] }),
    undefined,
  );
  assert.equal(query.placeholderData(previous, undefined), undefined);
  assert.equal(page.component('TopicsTable').props.busy, true);
});

test('Bỏ lọc clears applied and pending filters immediately and resets pagination', () => {
  const page = proposalPageFixture();
  page
    .find((node) => node.props.placeholder === 'Tên đề tài tiếng Việt hoặc tiếng Anh')
    .props.onChange({ target: { value: '  test  ' } });
  page.render();
  page.find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN').props.onChange('batch-1');
  page.render();
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài')
    .props.onChange('Draft');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  assert.deepEqual(page.query()[2], { keyword: 'test', batchId: 'batch-1', status: 'Draft' });
  page.component('TopicsTable').props.pagination.onPageChange(3);
  page.render();
  assert.equal(page.query()[3], 3);
  page
    .find((node) => node.props.placeholder === 'Tên đề tài tiếng Việt hoặc tiếng Anh')
    .props.onChange({ target: { value: 'pending change' } });
  page.render();
  page.button('Bỏ lọc').props.onClick();
  page.render();
  assert.deepEqual(page.query()[2], { keyword: '', batchId: '', status: '' });
  assert.equal(page.query()[3], 1);
  assert.equal(
    page.find((node) => node.props.placeholder === 'Tên đề tài tiếng Việt hoặc tiếng Anh').props
      .value,
    '',
  );
  assert.equal(
    page.find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN').props.value,
    '',
  );
  assert.equal(
    page.find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài').props.value,
    '',
  );
  assert.equal(page.button('Bỏ lọc').props.disabled, true);
});

test('status filters retain the batch, reset pagination and statistics support quick filters', () => {
  const page = proposalPageFixture();
  page.find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN').props.onChange('batch-1');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  page.component('TopicsTable').props.pagination.onPageChange(2);
  page.render();
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài')
    .props.onChange('NeedRevision');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  assert.deepEqual(page.query()[2], { keyword: '', batchId: 'batch-1', status: 'NeedRevision' });
  assert.equal(page.query()[3], 1);
  assert.ok(
    page.component('StatisticsBar').props.items.every((item) => typeof item.onClick === 'function'),
  );
  page.find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài').props.onChange('');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  assert.deepEqual(page.query()[2], { keyword: '', batchId: 'batch-1', status: '' });
});

test('proposal statistics apply a status without submitting the filter form', () => {
  const page = proposalPageFixture();
  page
    .component('StatisticsBar')
    .props.items.find((item) => item.key === 'revision')
    .onClick();
  page.render();
  assert.equal(page.query()[2].status, 'NeedRevision');
  assert.equal(page.query()[3], 1);
  assert.equal(
    page.component('StatisticsBar').props.items.find((item) => item.key === 'revision').active,
    true,
  );
  page
    .component('StatisticsBar')
    .props.items.find((item) => item.key === 'total')
    .onClick();
  page.render();
  assert.equal(page.query()[2].status, '');
});

test('preview preserves unsaved input and leaving/reloading requires a decision', () => {
  const page = proposalPageFixture();
  page.button('Đề xuất đề tài').props.onClick();
  page.render();
  assert.equal(page.shouldBlock(), false);
  page
    .find((node) => node.props.id === 'topic-title-vi')
    .props.onChange({ target: { value: 'Nội dung chưa lưu' } });
  page.render();
  assert.equal(page.shouldBlock(), true);
  let prevented = false;
  const event = {
    preventDefault: () => {
      prevented = true;
    },
    returnValue: undefined,
  };
  page.unload(event);
  assert.equal(prevented, true);
  assert.equal(event.returnValue, '');
  page.button('Xem trước đề tài').props.onClick();
  page.render();
  assert.equal(page.component('TopicPreview').props.values.titleVi, 'Nội dung chưa lưu');
  page.button('Quay lại chỉnh sửa').props.onClick();
  page.render();
  assert.equal(
    page.find((node) => node.props.id === 'topic-title-vi').props.value,
    'Nội dung chưa lưu',
  );
  assert.equal(page.mutationCalls(), 0);
  page.block();
  page.render();
  page.confirm().props.onClose();
  page.render();
  assert.equal(page.shouldBlock(), true);
  page.block();
  page.render();
  page.confirm().props.onConfirm();
  page.render();
  assert.equal(page.proceeds(), 1);
  assert.equal(page.shouldBlock(), false);
});

function draftPage() {
  const page = proposalPageFixture();
  page.button('Đề xuất đề tài').props.onClick();
  page.render();
  page
    .find((node) => node.props['aria-labelledby'] === 'topic-batch-label')
    .props.onChange('batch-1');
  page.render();
  page
    .find((node) => node.props.id === 'topic-title-vi')
    .props.onChange({ target: { value: 'Đề tài mới' } });
  page.render();
  return page;
}

function submitProposal(page, action) {
  page
    .find((node) => node.type === 'form')
    .props.onSubmit({
      preventDefault() {},
      nativeEvent: { submitter: { getAttribute: (name) => (name === 'value' ? action : null) } },
    });
  page.render();
}

test('assignment input validates MSSV and quota before either save action', () => {
  const page = draftPage();
  page.component('TopicStudentAssignmentField').props.onChange(['24521603', '24521604']);
  page.render();
  page.find((node) => node.props.id === 'topic-capacity-field').props.onChange(1);
  page.render();
  assert.deepEqual(page.component('TopicStudentAssignmentField').props.codes, [
    '24521603',
    '24521604',
  ]);
  submitProposal(page, 'draft');
  assert.match(page.component('TopicStudentAssignmentField').props.error, /vượt quá chỉ tiêu/);
  assert.equal(page.mutationCalls(), 0);
  page.flushFrames();
  assert.equal(page.focused.at(-1), 'topic-student-codes');
  page.component('TopicStudentAssignmentField').props.onChange(['SV01', ' sv01 ']);
  page.render();
  page.find((node) => node.props.id === 'topic-capacity-field').props.onChange(2);
  page.render();
  submitProposal(page, 'draft');
  assert.match(page.component('TopicStudentAssignmentField').props.error, /trùng/);
  page.component('TopicStudentAssignmentField').props.onChange(['24521603']);
  page.render();
  submitProposal(page, 'draft');
  assert.deepEqual(page.saveCalls()[0].values.studentCodes, ['24521603']);
});

test('saving a draft allows English title, description and requirements to remain unfinished', () => {
  const page = draftPage();
  submitProposal(page, 'draft');
  assert.equal(page.mutationCalls(), 1);
  const save = page.mutationVariables()[0];
  assert.equal(save.submitImmediately, false);
  assert.equal(save.values.titleEn, '');
  assert.equal(save.values.description, '');
  assert.equal(save.values.requirements, '');
});

test('sending requires every content field and reports all missing fields without saving', () => {
  const page = draftPage();
  submitProposal(page, 'submit');
  assert.equal(page.mutationCalls(), 0);
  const error = page.find(
    (node) =>
      node.props.role === 'alert' &&
      typeof node.props.children === 'string' &&
      node.props.children.startsWith('Vui lòng kiểm tra'),
  ).props.children;
  assert.match(error, /tên đề tài tiếng Anh/);
  assert.match(error, /mục tiêu và mô tả/);
  assert.match(error, /yêu cầu công nghệ và kiến thức/);
  assert.ok(page.find((node) => node.props.id === 'topic-title-en').props.error);
  assert.equal(
    page.find((node) => node.props.id === 'topic-description').props['aria-invalid'],
    true,
  );
  page.flushFrames();
  assert.equal(page.focused.at(-1), 'topic-title-en');
  for (const id of ['topic-title-en', 'topic-description', 'topic-requirements']) {
    page.find((node) => node.props.id === id).props.onChange({ target: { value: '   ' } });
    page.render();
  }
  submitProposal(page, 'submit');
  assert.equal(page.mutationCalls(), 0);
});

const fullTopic = {
  id: 'topic-1',
  advisorId: 'lecturer-1',
  batchId: 'batch-1',
  status: 'Draft',
  titleVi: 'Đề tài ban đầu',
  titleEn: 'Original topic',
  description: 'Mục tiêu đầy đủ',
  requirements: 'React, .NET',
  studentNumber: 2,
  createdAt: '2026-10-01T00:00:00Z',
  isPublished: false,
};

test('topic titles select the row; only row double-click or the view action opens details', () => {
  const { TopicsTable } = loadModule('features/topics/components/TopicsTable.tsx', {
    '@/components/ui/data-table': { DataTable },
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@/components/ui/button': { Button: 'Button' },
    '@/components/ui/checkbox': { Checkbox: 'Checkbox' },
    '@/components/ui/skeleton': { Skeleton: 'Skeleton' },
    '@/components/ui/selectable-table-row': { SelectableTableRow: 'SelectableTableRow' },
    '@/components/ui/selection-indicator': { SelectionIndicator: 'SelectionIndicator' },
    '@/components/ui/table': Object.fromEntries(
      ['Table', 'TableHeader', 'TableHead', 'TableBody', 'TableCell'].map((name) => [name, name]),
    ),
    '../types/topic.types': { canEditTopic: () => true },
    './TopicStatusBadge': { TopicStatusBadge: 'TopicStatusBadge' },
  });
  const { SelectableTableRow } = loadModule('components/ui/selectable-table-row.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn: (...classes) => classes.filter(Boolean).join(' ') },
  });
  const opened = [];
  const selected = [];
  const render = (busy = false) =>
    TopicsTable({
      topics: [fullTopic],
      batches: [],
      pageNumber: 1,
      pageSize: 10,
      busy,
      onOpen: (topic) => opened.push(topic.id),
      onSelectRow: (id) => selected.push(id),
      onCopy() {},
      onSubmit() {},
    });
  const tree = DataTable(render().props);
  const row = findElement(tree, (node) => node.type === 'SelectableTableRow');
  const title = findElement(row, (node) => node.props.children === fullTopic.titleVi);
  assert.equal(title.type, 'span');
  assert.equal(title.props.onClick, undefined);
  const titleEvent = { target: { closest: () => null } };
  SelectableTableRow(row.props).props.onClick(titleEvent);
  assert.deepEqual(selected, [fullTopic.id]);
  assert.deepEqual(opened, []);
  row.props.onDoubleClick(titleEvent);
  assert.deepEqual(opened, [fullTopic.id]);
  row.props.onDoubleClick({ target: { closest: () => ({ tagName: 'BUTTON' }) } });
  assert.equal(opened.length, 1);
  const busyRow = findElement(
    DataTable(render(true).props),
    (node) => node.type === 'SelectableTableRow',
  );
  busyRow.props.onDoubleClick(titleEvent);
  SelectableTableRow(busyRow.props).props.onClick(titleEvent);
  assert.equal(opened.length, 1);
  assert.equal(selected.length, 1);
  assert.equal(
    findElement(
      tree,
      (node) => node.type === 'button' && node.props.children === fullTopic.titleVi,
    ),
    undefined,
  );
});

test('proposal list uses the applied batch instead of editor state and offers single-row actions', () => {
  const changes = [];
  const page = proposalPageFixture({
    listedTopic: fullTopic,
    pageProps: {
      initialFilters: { keyword: '', batchId: 'batch-1', status: '' },
      onFiltersChange: (value) => changes.push(value),
    },
  });
  assert.equal(page.query()[2].batchId, 'batch-1');
  assert.doesNotMatch(JSON.stringify(page.render()), /Học kỳ hiện tại/);
  const table = page.component('TopicsTable');
  assert.equal(table.props.selectedIds, undefined);
  assert.equal(table.props.onToggleSelect, undefined);
  table.props.onSelectRow(fullTopic.id);
  page.render();
  assert.equal(page.component('TableFloatingBar').props.selectedCount, 1);
  page.component('TopicsTable').props.pagination.onPageChange(2);
  page.render();
  assert.equal(page.component('TableFloatingBar'), undefined);
  page.button('Bỏ lọc').props.onClick();
  page.render();
  assert.deepEqual(changes.at(-1), { keyword: '', batchId: '', status: '' });
});

test('proposal sorting sends the requested order to the server and resets page and selection', async () => {
  const page = proposalPageFixture({ listedTopic: fullTopic });
  page.component('TopicsTable').props.pagination.onPageChange(2);
  page.component('TopicsTable').props.onSelectRow(fullTopic.id);
  page.render();
  page.component('TopicsTable').props.onSortChange('title');
  page.render();
  assert.deepEqual(page.listOptions().queryKey.slice(3), [1, 20, 'title', 'asc']);
  assert.equal(page.component('TopicsTable').props.selectedRowId, null);
  page.component('TopicsTable').props.onSortChange('title');
  page.render();
  assert.deepEqual(page.listOptions().queryKey.slice(5), ['title', 'desc']);
});

test('legacy drafts with no capacity save the displayed capacity before table or form submission', async () => {
  const legacy = { ...fullTopic, studentNumber: null };
  const shortcut = proposalPageFixture({ listedTopic: legacy });
  shortcut.component('TopicsTable').props.onSubmit(legacy);
  await shortcut.settle();
  shortcut.render();
  shortcut.confirm().props.onConfirm();
  assert.equal(shortcut.saveCalls()[0].hasChanges, true);
  await shortcut.executeSave();
  assert.equal(shortcut.apiCalls[0][0], 'save');
  assert.equal(shortcut.apiCalls[0][2].studentNumber, 2);

  const form = proposalPageFixture({ listedTopic: legacy });
  form.component('TopicsTable').props.onOpen(legacy);
  await form.settle();
  form.render();
  submitProposal(form, 'submit');
  form.render();
  form.confirm().props.onConfirm();
  await form.executeSave();
  assert.equal(form.apiCalls[0][0], 'save');
  assert.equal(form.apiCalls[0][2].studentNumber, 2);
});

test('saving from a later filtered page resets pagination and refreshes both proposal and review caches', async () => {
  const page = proposalPageFixture({ listedTopic: fullTopic });
  page.component('TopicsTable').props.pagination.onPageChange(3);
  page.render();
  page.component('TopicsTable').props.onSubmit(fullTopic);
  await page.settle();
  page.render();
  page.confirm().props.onConfirm();
  await page.completeSave({ ...fullTopic, status: 'Submitted' });
  page.render();
  assert.equal(page.query()[3], 1);
  assert.deepEqual(page.invalidations, [
    ['my-proposed-topics'],
    ['topic-review'],
    ['topic-advising'],
    ['topic-advising-group-detail'],
  ]);
});

test('missing batch-read permission is explained rather than displayed as an endless loading state', () => {
  const page = proposalPageFixture({ canReadBatches: false });
  page.button('Đề xuất đề tài').props.onClick();
  page.render();
  const selector = page.find((node) => node.props.id === 'topic-batch-field');
  assert.equal(selector.props.disabled, true);
  assert.equal(selector.props.placeholder, 'Chưa có quyền xem đợt KLTN');
  assert.ok(
    page.find(
      (node) =>
        node.props.role === 'alert' &&
        String(node.props.children).includes('chưa có quyền xem danh sách đợt'),
    ),
  );
});

test('local drafts are isolated by account and topic and malformed drafts are ignored', () => {
  const { saveLocalProposal, readLocalProposal, removeLocalProposal, EMPTY_PROPOSAL } = loadModule(
    'features/topics/proposalEditing.ts',
  );
  const entries = new Map();
  const storage = {
    getItem: (key) => entries.get(key) ?? null,
    setItem: (key, value) => entries.set(key, value),
    removeItem: (key) => entries.delete(key),
  };
  const values = { ...EMPTY_PROPOSAL, titleVi: 'Chưa lưu' };
  saveLocalProposal('lecturer-1', null, values, EMPTY_PROPOSAL, storage);
  saveLocalProposal(
    'lecturer-1',
    'topic-1',
    { ...values, description: 'Bản sửa' },
    values,
    storage,
  );
  assert.equal(readLocalProposal('lecturer-1', null, storage).values.titleVi, 'Chưa lưu');
  assert.equal(readLocalProposal('lecturer-2', null, storage), null);
  assert.equal(readLocalProposal('lecturer-1', 'topic-2', storage), null);
  removeLocalProposal('lecturer-1', null, storage);
  assert.equal(readLocalProposal('lecturer-1', null, storage), null);
  assert.equal(readLocalProposal('lecturer-1', 'topic-1', storage).values.description, 'Bản sửa');
  const [key] = entries.keys();
  entries.set(key, '{broken');
  assert.equal(readLocalProposal('lecturer-1', 'topic-1', storage), null);
  entries.set(key, JSON.stringify({ version: 1, values: {}, baseline: {}, savedAt: 'wrong' }));
  assert.equal(readLocalProposal('lecturer-1', 'topic-1', storage), null);
});

test('autosaved new content survives a reload and is removed only after successful server save', async () => {
  const page = draftPage();
  page
    .find((node) => node.props.id === 'topic-description')
    .props.onChange({ target: { value: 'Nội dung dài chưa gửi' } });
  page.render();
  page.flushTimers();
  page.render();
  assert.ok(page.editing.readLocalProposal('lecturer-1', null));
  // readLocalProposal's captured storage is the fixture's browser storage.
  const reopened = proposalPageFixture({ saved: page.saved });
  reopened.button('Khôi phục nội dung').props.onClick();
  reopened.render();
  assert.equal(
    reopened.find((node) => node.props.id === 'topic-description').props.value,
    'Nội dung dài chưa gửi',
  );
  assert.equal(reopened.shouldBlock(), true);
  submitProposal(reopened, 'draft');
  assert.equal(reopened.saveCalls().length, 1);
  assert.ok(reopened.editing.readLocalProposal('lecturer-1', null));
  await reopened.completeSave(fullTopic);
  reopened.render();
  assert.equal(reopened.editing.readLocalProposal('lecturer-1', null), null);
});

test('copying a published topic creates a new proposal with no batch or approval state', async () => {
  const source = { ...fullTopic, status: 'Published', isPublished: true };
  const page = proposalPageFixture({ listedTopic: source });
  page.component('TopicsTable').props.onCopy(source);
  await page.settle();
  page.render();
  assert.equal(
    page.find((node) => node.props['aria-labelledby'] === 'topic-batch-label').props.value,
    '',
  );
  assert.equal(page.component('TopicStatusBadge').props.status, 'Draft');
  assert.equal(page.find((node) => node.props.id === 'topic-title-vi').props.value, source.titleVi);
  assert.equal(page.saveCalls().length, 0);
  page
    .find((node) => node.props['aria-labelledby'] === 'topic-batch-label')
    .props.onChange('batch-2');
  page.render();
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls()[0].id, null);
  assert.equal(page.saveCalls()[0].values.batchId, 'batch-2');
  assert.equal('status' in page.saveCalls()[0].values, false);
});

test('send from table refreshes the topic and refuses to send a status changed on the server', async () => {
  const page = proposalPageFixture({
    listedTopic: fullTopic,
    serverTopic: { ...fullTopic, status: 'Submitted' },
  });
  page.component('TopicsTable').props.onSubmit(fullTopic);
  await page.settle();
  page.render();
  assert.equal(page.saveCalls().length, 0);
  assert.equal(page.component('Dialog'), undefined);
});

test('send from table validates fresh content, opens missing fields, and submits complete content', async () => {
  const incomplete = proposalPageFixture({
    listedTopic: fullTopic,
    serverTopic: { ...fullTopic, titleEn: '', description: '' },
  });
  incomplete.component('TopicsTable').props.onSubmit(fullTopic);
  await incomplete.settle();
  incomplete.render();
  assert.equal(incomplete.saveCalls().length, 0);
  assert.ok(incomplete.find((node) => node.props.id === 'topic-title-en').props.error);
  assert.equal(
    incomplete.find((node) => node.props.id === 'topic-description').props['aria-invalid'],
    true,
  );
  const complete = proposalPageFixture({ listedTopic: fullTopic });
  complete.component('TopicsTable').props.onSubmit(fullTopic);
  await complete.settle();
  complete.render();
  complete.confirm().props.onConfirm();
  assert.deepEqual(complete.saveCalls()[0], {
    id: fullTopic.id,
    values: complete.editing.topicToValues(fullTopic),
    submitImmediately: true,
    hasChanges: false,
  });
});

test('table submit cannot bypass a locally edited draft and recovery preserves the immutable batch', async () => {
  const page = proposalPageFixture({ listedTopic: fullTopic });
  const baseline = page.editing.topicToValues(fullTopic);
  page.editing.saveLocalProposal(
    'lecturer-1',
    fullTopic.id,
    { ...baseline, batchId: 'wrong-batch', description: 'Bản sửa trên thiết bị' },
    baseline,
  );
  page.component('TopicsTable').props.onSubmit(fullTopic);
  await page.settle();
  page.render();
  assert.equal(page.saveCalls().length, 0);
  assert.ok(page.find((node) => node.type === 'fieldset').props.disabled);
  page.button('Khôi phục nội dung').props.onClick();
  page.render();
  assert.equal(
    page.find((node) => node.props['aria-labelledby'] === 'topic-batch-label').props.value,
    fullTopic.batchId,
  );
  assert.equal(
    page.find((node) => node.props.id === 'topic-description').props.value,
    'Bản sửa trên thiết bị',
  );
  assert.equal(page.shouldBlock(), true);
});

test('removing one active filter chip retains the other filters and resets pagination', () => {
  const page = proposalPageFixture();
  page
    .find((node) => node.props.placeholder === 'Tên đề tài tiếng Việt hoặc tiếng Anh')
    .props.onChange({ target: { value: 'test' } });
  page.render();
  page.find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN').props.onChange('batch-1');
  page.render();
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài')
    .props.onChange('Draft');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  page.component('TopicsTable').props.pagination.onPageChange(3);
  page.render();
  page
    .component('TableActiveFilters')
    .props.chips.find((chip) => chip.key === 'status')
    .onRemove();
  page.render();
  assert.deepEqual(page.query()[2], { keyword: 'test', batchId: 'batch-1', status: '' });
  assert.equal(page.query()[3], 1);
  assert.equal(
    page.find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài').props.value,
    '',
  );
});

test('a storage quota failure preserves form content and does not block saving to the server', () => {
  const page = proposalPageFixture({ failLocalWrite: true });
  page.button('Đề xuất đề tài').props.onClick();
  page.render();
  page
    .find((node) => node.props['aria-labelledby'] === 'topic-batch-label')
    .props.onChange('batch-1');
  page.render();
  page
    .find((node) => node.props.id === 'topic-title-vi')
    .props.onChange({ target: { value: 'Vẫn giữ nội dung' } });
  page.render();
  page.flushTimers();
  page.render();
  assert.ok(
    page.find(
      (node) =>
        typeof node.props.children === 'string' &&
        node.props.children.startsWith('Chưa thể lưu tạm'),
    ),
  );
  assert.equal(
    page.find((node) => node.props.id === 'topic-title-vi').props.value,
    'Vẫn giữ nội dung',
  );
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls().length, 1);
});

test('discarding a form cancels pending autosave so it cannot recreate a deleted local draft', () => {
  const page = draftPage();
  page.button('Đóng').props.onClick();
  page.render();
  page.confirm().props.onConfirm();
  page.render();
  page.flushTimers();
  assert.equal(page.editing.readLocalProposal('lecturer-1', null), null);
});

test('reverting all changes removes the obsolete local draft instead of offering it after reload', () => {
  const page = proposalPageFixture();
  page.button('Đề xuất đề tài').props.onClick();
  page.render();
  page
    .find((node) => node.props.id === 'topic-title-vi')
    .props.onChange({ target: { value: 'Sửa thử' } });
  page.render();
  page.flushTimers();
  page.render();
  assert.ok(page.editing.readLocalProposal('lecturer-1', null));
  page.find((node) => node.props.id === 'topic-title-vi').props.onChange({ target: { value: '' } });
  page.render();
  assert.equal(page.shouldBlock(), false);
  page.flushTimers();
  assert.equal(page.editing.readLocalProposal('lecturer-1', null), null);
});

test('an outdated local draft is never restored into a submitted read-only topic', async () => {
  const page = proposalPageFixture({
    listedTopic: fullTopic,
    serverTopic: { ...fullTopic, status: 'Submitted' },
  });
  const initial = page.editing.topicToValues(fullTopic);
  page.editing.saveLocalProposal(
    'lecturer-1',
    fullTopic.id,
    { ...initial, titleVi: 'Sửa chưa gửi' },
    initial,
  );
  page.component('TopicsTable').props.onOpen(fullTopic);
  await page.settle();
  page.render();
  assert.equal(page.button('Khôi phục nội dung'), undefined);
  assert.equal(page.find((node) => node.type === 'fieldset').props.disabled, true);
  assert.equal(
    page.find((node) => node.props.id === 'topic-title-vi').props.value,
    fullTopic.titleVi,
  );
  assert.equal(page.shouldBlock(), false);
});

test('sending becomes available only after all three additional fields have content', () => {
  const page = draftPage();
  for (const [index, id] of [
    'topic-title-en',
    'topic-description',
    'topic-requirements',
  ].entries()) {
    page
      .find((node) => node.props.id === id)
      .props.onChange({ target: { value: 'Nội dung đầy đủ' } });
    page.render();
    submitProposal(page, 'submit');
    if (index === 2) {
      page.render();
      page.confirm().props.onConfirm();
    }
    assert.equal(page.mutationCalls(), index === 2 ? 1 : 0);
  }
  assert.equal(page.mutationVariables()[0].submitImmediately, true);
});

const assignedTopic = {
  ...fullTopic,
  group: {
    groupId: 'group-1',
    status: 'Registered',
    createdAt: fullTopic.createdAt,
    students: [
      {
        studentId: 'student-1',
        userCode: '24521603',
        fullName: 'Nguyễn Minh Đức',
        email: 'student1@example.test',
        phoneNumber: null,
        joinedAt: fullTopic.createdAt,
      },
    ],
  },
};

test('editing reads the assigned roster and changes it only with an explicit assignment edit', async () => {
  const page = proposalPageFixture({ listedTopic: assignedTopic });
  page.component('TopicsTable').props.onOpen(assignedTopic);
  await page.settle();
  page.render();
  assert.deepEqual(page.component('TopicStudentAssignmentField').props.codes, ['24521603']);
  page
    .find((node) => node.props.id === 'topic-description')
    .props.onChange({ target: { value: 'Chỉ sửa nội dung' } });
  page.render();
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls()[0].updateAssignedStudents, undefined);
  await page.executeSave();
  assert.equal(page.apiCalls[0].length, 4); // operation, id, values, submit flag; no roster update flag.

  page.component('TopicStudentAssignmentField').props.onChange(['24521603', '24521604']);
  page.render();
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls().at(-1).updateAssignedStudents, true);
  await page.executeSave();
  assert.equal(page.apiCalls.at(-1)[4], true);
});

test('clearing assigned students is retained as a pending edit until saving', async () => {
  const page = proposalPageFixture({ listedTopic: assignedTopic });
  page.component('TopicsTable').props.onOpen(assignedTopic);
  await page.settle();
  page.render();
  page.component('TopicStudentAssignmentField').props.onChange([]);
  page.render();
  assert.equal(page.shouldBlock(), true);
  assert.equal(page.apiCalls.length, 0);
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls()[0].updateAssignedStudents, true);
  assert.deepEqual(page.saveCalls()[0].values.studentCodes, []);
});

test('editing a one-person quota explains the backend limitation before assigning two students', async () => {
  const limited = { ...assignedTopic, studentNumber: 1 };
  const page = proposalPageFixture({ listedTopic: limited });
  page.component('TopicsTable').props.onOpen(limited);
  await page.settle();
  page.render();
  page.find((node) => node.props.id === 'topic-capacity-field').props.onChange(2);
  page.render();
  page.component('TopicStudentAssignmentField').props.onChange(['24521603', '24521604']);
  page.render();
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls().length, 0);
  assert.match(page.component('TopicStudentAssignmentField').props.error, /lưu chỉ tiêu mới trước/);
});

test('assigned MSSV survive preview and local draft recovery, but never copy to another topic', async () => {
  const page = draftPage();
  page.component('TopicStudentAssignmentField').props.onChange(['24521603']);
  page.render();
  page.button('Xem trước đề tài').props.onClick();
  page.render();
  assert.deepEqual(page.component('TopicPreview').props.values.studentCodes, ['24521603']);
  page.button('Quay lại chỉnh sửa').props.onClick();
  page.render();
  page.flushTimers();
  const reloaded = proposalPageFixture({ saved: page.saved });
  reloaded.button('Khôi phục nội dung').props.onClick();
  reloaded.render();
  assert.deepEqual(reloaded.component('TopicStudentAssignmentField').props.codes, ['24521603']);

  const copy = proposalPageFixture({ listedTopic: assignedTopic });
  copy.component('TopicsTable').props.onCopy(assignedTopic);
  await copy.settle();
  copy.render();
  assert.deepEqual(copy.component('TopicStudentAssignmentField').props.codes, []);
});

test('recovering an older draft without studentCodes preserves the actual server roster', async () => {
  const page = proposalPageFixture({ listedTopic: assignedTopic });
  const legacyBaseline = page.editing.topicToValues(assignedTopic);
  delete legacyBaseline.studentCodes;
  page.editing.saveLocalProposal(
    'lecturer-1',
    assignedTopic.id,
    { ...legacyBaseline, description: 'Nội dung lưu tạm cũ' },
    legacyBaseline,
  );
  page.component('TopicsTable').props.onOpen(assignedTopic);
  await page.settle();
  page.render();
  page.button('Khôi phục nội dung').props.onClick();
  page.render();
  assert.deepEqual(page.component('TopicStudentAssignmentField').props.codes, ['24521603']);
  submitProposal(page, 'draft');
  assert.equal(page.saveCalls()[0].updateAssignedStudents, undefined);
});

test('MSSV field retains excess entries after quota reduction and locks incomplete server identities', () => {
  const { TopicStudentAssignmentField } = loadModule(
    'features/topics/components/TopicStudentAssignmentField.tsx',
    {
      'react/jsx-runtime': jsxRuntime,
      'lucide-react': icons,
      '@/components/ui/input': { Input: 'Input' },
      '@/components/ui/button': { Button: 'Button' },
    },
  );
  const walk = (node, type) =>
    !React.isValidElement(node)
      ? []
      : [
          ...(node.type === type ? [node] : []),
          ...React.Children.toArray(node.props.children).flatMap((child) => walk(child, type)),
        ];
  const props = { codes: ['24521603', '24521604'], capacity: 1, onChange() {} };
  assert.equal(walk(TopicStudentAssignmentField(props), 'Input').length, 2);
  const unknown = TopicStudentAssignmentField({
    ...props,
    currentStudents: [{ ...assignedTopic.group.students[0], userCode: null }],
  });
  assert.ok(walk(unknown, 'Input').every((input) => input.props.disabled));
});

test('student lookup API normalizes MSSV, returns null on 404 and field renders student names', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (...args) => {
      calls.push(args);
      if (args[1].params.userCode === '24521603') {
        return success({ userCode: '24521603', fullName: 'Nguyễn Văn A' });
      }
      const err = new Error('Not found');
      err.isAxiosError = true;
      err.response = { status: 404 };
      throw err;
    },
  });
  const signal = new AbortController().signal;
  assert.deepEqual(await topicApi.lookupStudentByCode('  24521603 ', signal), {
    userCode: '24521603',
    fullName: 'Nguyễn Văn A',
  });
  assert.deepEqual(calls[0], [
    '/api/topics/student-lookup',
    { params: { userCode: '24521603' }, signal },
  ]);
  assert.equal(await topicApi.lookupStudentByCode('24529999'), null);
  assert.equal(await topicApi.lookupStudentByCode('   '), null);

  const { TopicStudentAssignmentField } = loadModule(
    'features/topics/components/TopicStudentAssignmentField.tsx',
    {
      'react/jsx-runtime': jsxRuntime,
      'lucide-react': icons,
      '@/components/ui/input': { Input: 'input' },
      '@/components/ui/button': { Button: 'button' },
    },
  );
  const markup = renderToStaticMarkup(
    TopicStudentAssignmentField({
      codes: ['24521603', '24529999'],
      capacity: 2,
      studentLookups: {
        '24521603': { status: 'found', fullName: 'Nguyễn Văn A' },
        '24529999': { status: 'not_found' },
      },
      onChange() {},
    }),
  );
  assert.match(markup, /Nguyễn Văn A/);
  assert.match(markup, /Không tìm thấy sinh viên với MSSV này/);
});
