import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import axios from 'axios';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';

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

test('statistics use server totals across all pages and retain advisor/batch scope', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (path, options) => {
      calls.push([path, options]);
      const totals = { Submitted: 15, NeedRevision: 7 };
      return success({
        items: [{ id: 'one-row' }],
        totalCount: totals[options.params.status] ?? 84,
      });
    },
  });
  const signal = new AbortController().signal;
  assert.deepEqual(await topicApi.getStatistics('admin-1', 'batch-1', signal), {
    total: 84,
    submitted: 15,
    needRevision: 7,
  });
  assert.equal(calls.length, 3);
  assert.deepEqual(
    calls.map(([, options]) => options.params.status),
    [undefined, 'Submitted', 'NeedRevision'],
  );
  for (const [path, options] of calls) {
    assert.equal(path, '/api/topics/my-topics');
    assert.equal(options.params.advisorId, 'admin-1');
    assert.equal(options.params.batchId, 'batch-1');
    assert.equal(options.params.pageNumber, 1);
    assert.equal(options.params.pageSize, 1);
    assert.equal(options.signal, signal);
  }
});

test('empty filters are omitted and failed statistics do not silently display zero', async () => {
  const calls = [];
  const { topicApi } = apiFixture({
    get: async (_path, options) => {
      calls.push(options.params);
      if (options.params.status === 'NeedRevision') throw new Error('Offline');
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

const { navigationSections } = loadModule('shared/layout/navigationConfig.ts', {
  'lucide-react': icons,
});

function renderSidebar(roles) {
  const Link = ({ to, children, ...props }) =>
    React.createElement('a', { ...props, href: to }, children);
  const { AppLayout } = loadModule('shared/layout/AppLayout.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': {
      Link,
      NavLink: Link,
      Outlet: () => null,
      useLocation: () => ({ pathname: '/topics/propose' }),
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
  });
  return renderToStaticMarkup(React.createElement(AppLayout));
}

test('actual AppLayout renders the proposal link for Lecturer and Admin, including mixed roles', () => {
  for (const roles of [
    ['Lecturer'],
    ['lecturer'],
    ['Admin'],
    ['Faculty', 'Lecturer'],
    ['Admin', 'Lecturer'],
  ]) {
    const markup = renderSidebar(roles);
    assert.match(markup, /href="\/topics\/propose"/);
    assert.match(markup, /Đề xuất đề tài/);
    assert.doesNotMatch(markup, /Không có phân hệ nào khả dụng/);
  }
});

test('actual AppLayout hides the proposal link from accounts without Lecturer or Admin', () => {
  for (const roles of [['Student'], ['Faculty'], ['CouncilMember'], []]) {
    assert.doesNotMatch(renderSidebar(roles), /href="\/topics\/propose"/);
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
    'StatisticsBar',
    'Card',
    'CardContent',
    'CardHeader',
    'CardTitle',
    'TopicStatusBadge',
    'TopicsTable',
    'TopicPreview',
    'TopicProposalSchedule',
    'TablePagination',
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
        useQueryClient: () => ({ invalidateQueries: async () => {} }),
        useQuery: (queryOptions) => {
          if (typeof queryOptions.queryKey[2] === 'object') listQuery = queryOptions.queryKey;
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
      '@/components/ui/statistics-bar': dependency('StatisticsBar'),
      '@/components/ui/card': Object.fromEntries(
        ['Card', 'CardContent', 'CardHeader', 'CardTitle'].map((key) => [key, components[key]]),
      ),
      '@/components/ui/table-pagination': dependency('TablePagination'),
      '../components/TopicStatusBadge': dependency('TopicStatusBadge'),
      '../components/TopicsTable': dependency('TopicsTable'),
      '../components/TopicPreview': dependency('TopicPreview'),
      '../components/TopicProposalSchedule': dependency('TopicProposalSchedule'),
      '../proposalEditing': editing,
      '@/shared/auth': { useAuth: () => ({ user: { id: 'lecturer-1', fullName: 'Giảng viên' } }) },
      '@/shared/academic-periods/useAcademicBatches': {
        useAcademicBatches: () => ({ data: [], isPending: false, isError: false }),
      },
      '../types/topic.types': loadModule('features/topics/types/topic.types.ts'),
      '../services/topicApi': {
        topicApi: { getDetail: async () => options.serverTopic ?? options.listedTopic },
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
      tree = TopicProposePage();
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
    query: () => listQuery,
    unload: (event) => beforeUnload(event),
    shouldBlock: () => shouldBlock,
    block: () => {
      blockerState = 'blocked';
    },
    proceeds: () => proceeds,
    mutationCalls: () => mutationCalls,
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
  page.component('TablePagination').props.onPageChange(3);
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

test('statistic shortcuts apply immediately, retain the applied batch and clear search', () => {
  const page = proposalPageFixture();
  page.find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN').props.onChange('batch-1');
  page.render();
  page.find((node) => node.type === 'form').props.onSubmit({ preventDefault() {} });
  page.render();
  page.component('TablePagination').props.onPageChange(2);
  page.render();
  page
    .component('StatisticsBar')
    .props.items.find((item) => item.key === 'revision')
    .onClick();
  page.render();
  assert.deepEqual(page.query()[2], { keyword: '', batchId: 'batch-1', status: 'NeedRevision' });
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
  assert.deepEqual(page.query()[2], { keyword: '', batchId: 'batch-1', status: '' });
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
  page.button('Tiếp tục soạn').props.onClick();
  page.render();
  assert.equal(page.shouldBlock(), true);
  page.block();
  page.render();
  page.button('Bỏ thay đổi và rời trang').props.onClick();
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
  page.component('TablePagination').props.onPageChange(3);
  page.render();
  page.find((node) => node.props['aria-label'] === 'Bỏ lọc trạng thái').props.onClick();
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
    assert.equal(page.mutationCalls(), index === 2 ? 1 : 0);
  }
  assert.equal(page.mutationVariables()[0].submitImmediately, true);
});
