import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import axios from 'axios';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';

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
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
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

test('batch dropdown fetches all pages and preserves cancellation', async () => {
  const calls = [];
  const signal = new AbortController().signal;
  const { topicApi } = apiFixture({
    get: async (path, options) => {
      calls.push([path, options]);
      const page = options.params.pageNumber;
      return success({ items: [{ id: `batch-${page}` }], hasNextPage: page === 1 });
    },
  });
  assert.deepEqual(await topicApi.getBatches(signal), [{ id: 'batch-1' }, { id: 'batch-2' }]);
  assert.equal(calls.length, 2);
  assert.equal(calls[1][0], '/api/academic-periods/batches');
  assert.deepEqual(calls[1][1], { params: { pageNumber: 2, pageSize: 100 }, signal });
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

test('only Draft and NeedRevision permit editing', () => {
  const { canEditTopic } = loadModule('features/topics/types/topic.types.ts');
  for (const status of ['Draft', 'NeedRevision']) assert.equal(canEditTopic(status), true);
  for (const status of ['Submitted', 'Approved', 'Rejected', 'Published', 'Closed', 'Archived']) {
    assert.equal(canEditTopic(status), false);
  }
});
