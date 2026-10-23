import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

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
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    browserWindow,
  );
  return exports;
}

const fullTopic = {
  id: 'topic-1',
  batchId: 'batch-1',
  advisorId: 'lecturer-1',
  titleVi: 'Phân tích dữ liệu',
  titleEn: 'Data analysis',
  description: 'Mục tiêu',
  requirements: 'Python',
  studentNumber: 2,
  createdAt: '2026-10-20T00:00:00Z',
  status: 'Submitted',
  isPublished: false,
};
const success = (data) => ({ data: { success: true, data } });
function apiFixture({ client = {}, topics = {}, users = {} } = {}) {
  return loadModule('features/topics/services/topicReviewApi.ts', {
    '@/shared/api': { httpClient: client },
    '@/features/admin/services/userApi': { userApi: users },
    './topicApi': { topicApi: topics },
    '../types/topic.types': loadModule('features/topics/types/topic.types.ts'),
  }).topicReviewApi;
}

test('faculty queue and statistics use all advisors, batch scope, server totals and cancellation', async () => {
  const requests = [];
  const signal = new AbortController().signal;
  const api = apiFixture({
    topics: {
      getMyTopics: async (query, token) => {
        requests.push(query);
        assert.equal(token, signal);
        assert.equal('advisorId' in query, false);
        return {
          items: [],
          totalCount:
            {
              Submitted: 25,
              Approved: 10,
              Published: 40,
              NeedRevision: 3,
              Rejected: 2,
              Closed: 1,
              Archived: 0,
            }[query.status] ?? 100,
        };
      },
    },
  });
  const query = {
    keyword: 'AI',
    status: 'Submitted',
    batchId: 'batch-2',
    pageNumber: 3,
    pageSize: 20,
  };
  await api.getTopics(query, signal);
  assert.deepEqual(requests[0], query);
  assert.deepEqual(await api.getStatistics('batch-2', signal), {
    total: 81,
    Submitted: 25,
    Approved: 10,
    Published: 40,
  });
  assert.ok(
    requests
      .slice(1)
      .every(
        (request) =>
          request.batchId === 'batch-2' && request.keyword === '' && request.pageSize === 1,
      ),
  );
});

test('all review statuses merge before pagination and never request lecturer drafts', async () => {
  const topics = Array.from({ length: 205 }, (_, index) => ({
    ...fullTopic,
    id: `submitted-${String(index).padStart(3, '0')}`,
    createdAt: new Date(Date.UTC(2026, 0, 1) + index * 1000).toISOString(),
  }));
  topics.push(
    ...['Approved', 'NeedRevision', 'Rejected', 'Published', 'Closed', 'Archived', 'Draft'].map(
      (status, index) => ({
        ...fullTopic,
        id: `other-${index}`,
        status,
        createdAt: new Date(Date.UTC(2026, 0, 1) + 100500 + index).toISOString(),
      }),
    ),
  );
  const requests = [];
  const signal = new AbortController().signal;
  const sort = (a, b) =>
    Date.parse(b.createdAt) - Date.parse(a.createdAt) || (a.id > b.id ? -1 : 1);
  const api = apiFixture({
    topics: {
      getMyTopics: async (query, token) => {
        requests.push(query);
        assert.equal(token, signal);
        assert.ok(query.status && query.status !== 'Draft');
        assert.equal(query.keyword, 'dữ liệu');
        assert.equal(query.batchId, 'batch-1');
        const matches = topics.filter((topic) => topic.status === query.status).sort(sort);
        const start = (query.pageNumber - 1) * query.pageSize;
        return { items: matches.slice(start, start + query.pageSize), totalCount: matches.length };
      },
    },
  });
  const result = await api.getTopics(
    { keyword: 'dữ liệu', batchId: 'batch-1', status: '', pageNumber: 12, pageSize: 10 },
    signal,
  );
  const visible = topics.filter((topic) => topic.status !== 'Draft').sort(sort);
  assert.deepEqual(result.items, visible.slice(110, 120));
  assert.equal(result.totalCount, 211);
  assert.equal(result.totalPages, 22);
  assert.equal(result.hasPreviousPage, true);
  assert.equal(result.hasNextPage, true);
  assert.ok(requests.some((query) => query.status === 'Submitted' && query.pageNumber === 2));
  const before = requests.length;
  assert.equal(
    (
      await api.getTopics({
        keyword: '',
        batchId: '',
        status: 'Draft',
        pageNumber: 1,
        pageSize: 20,
      })
    ).totalCount,
    0,
  );
  assert.equal(requests.length, before);
});

test('review detail refuses to show a lecturer draft even when its ID is known', async () => {
  const api = apiFixture({
    topics: { getDetail: async () => ({ ...fullTopic, status: 'Draft' }) },
  });
  await assert.rejects(api.getDetail('topic-1'), /chưa được gửi thẩm định/);
});

test('all conclusions send feedback and decision after checking the current status', async () => {
  const posts = [];
  const reads = [];
  const api = apiFixture({
    topics: {
      getDetail: async (id) => {
        reads.push(id);
        return fullTopic;
      },
    },
    client: {
      post: async (...args) => {
        posts.push(args);
        return success({ ...fullTopic, status: args[1].decision });
      },
    },
  });
  for (const decision of ['Approved', 'NeedRevision', 'Rejected']) {
    assert.equal(
      (await api.review('topic-1', decision, '  Cần bổ sung đánh giá.  ')).status,
      decision,
    );
  }
  assert.deepEqual(reads, ['topic-1', 'topic-1', 'topic-1']);
  assert.deepEqual(
    posts,
    ['Approved', 'NeedRevision', 'Rejected'].map((decision) => [
      '/api/topics/topic-1/review',
      { decision, reviewNote: 'Cần bổ sung đánh giá.' },
    ]),
  );
  await assert.rejects(api.review('topic-1', 'Published'), /không hợp lệ/);
  assert.equal(posts.length, 3);
  for (const decision of ['NeedRevision', 'Rejected']) {
    await assert.rejects(api.review('topic-1', decision, '  '), /Vui lòng nhập góp ý/);
  }
  await assert.rejects(api.review('topic-1', 'Approved', 'x'.repeat(2001)), /2000/);
  assert.equal(posts.length, 3);
});

test('review refuses every non-submitted state without posting a conclusion', async () => {
  let status;
  const api = apiFixture({
    topics: { getDetail: async () => ({ ...fullTopic, status }) },
    client: { post: () => assert.fail('Must not review a stale/non-submitted topic') },
  });
  for (status of [
    'Draft',
    'NeedRevision',
    'Approved',
    'Rejected',
    'Published',
    'Closed',
    'Archived',
  ]) {
    await assert.rejects(api.review('topic-1', 'Approved'), /Trạng thái đề tài đã thay đổi/);
  }
});

test('publication sends explicit deduplicated IDs and never uses an empty batch-wide request', async () => {
  const posts = [];
  const api = apiFixture({
    topics: { getDetail: async (id) => ({ ...fullTopic, id, status: 'Approved' }) },
    client: {
      post: async (...args) => {
        posts.push(args);
        return success({
          batchId: 'batch-1',
          publishedCount: 2,
          publishedTopicIds: ['topic-1', 'topic-2'],
        });
      },
    },
  });
  const result = await api.publish('batch-1', ['topic-1', 'topic-2', 'topic-1']);
  assert.equal(result.publishedCount, 2);
  assert.deepEqual(posts, [
    ['/api/topics/publish', { batchId: 'batch-1', topicIds: ['topic-1', 'topic-2'] }],
  ]);
  await assert.rejects(api.publish('batch-1', []), /Chọn ít nhất/);
  await assert.rejects(api.publish('', ['topic-1']), /Chọn ít nhất/);
  await assert.rejects(api.publish('batch-1', ['']), /Chọn ít nhất/);
  assert.equal(posts.length, 1);
});

test('publication aborts the entire request for a mixed batch or changed topic status', async () => {
  let changed = { batchId: 'batch-2', status: 'Approved' };
  const api = apiFixture({
    topics: {
      getDetail: async (id) => ({
        ...fullTopic,
        id,
        ...(id === 'topic-2' ? changed : { status: 'Approved' }),
      }),
    },
    client: { post: () => assert.fail('Must not publish a mixed or stale selection') },
  });
  await assert.rejects(api.publish('batch-1', ['topic-1', 'topic-2']), /Danh sách đã thay đổi/);
  changed = { batchId: 'batch-1', status: 'Published' };
  await assert.rejects(api.publish('batch-1', ['topic-1', 'topic-2']), /Danh sách đã thay đổi/);
});

test('server review and publication failures surface their messages', async () => {
  const api = apiFixture({
    topics: { getDetail: async () => fullTopic },
    client: {
      post: async () => ({
        data: { success: false, error: { message: 'Không đủ quyền thẩm định.' } },
      }),
    },
  });
  await assert.rejects(api.review('topic-1', 'Approved'), /Không đủ quyền/);
});

test('advisor names are resolved across staff pages and preserve cancellation', async () => {
  const signal = new AbortController().signal;
  const api = apiFixture({
    users: {
      getUsers: async (query, token) => {
        assert.equal(token, signal);
        assert.equal(query.accountGroup, 'Staff');
        assert.equal(query.status, '');
        return {
          items: [{ id: `gv-${query.pageNumber}`, fullName: `Giảng viên ${query.pageNumber}` }],
          hasNextPage: query.pageNumber === 1,
        };
      },
    },
  });
  assert.deepEqual(await api.getAdvisorNames(signal), {
    'gv-1': 'Giảng viên 1',
    'gv-2': 'Giảng viên 2',
  });
});

function walk(node, predicate) {
  if (Array.isArray(node)) return node.flatMap((child) => walk(child, predicate));
  if (!React.isValidElement(node)) return [];
  return [...(predicate(node) ? [node] : []), ...walk(node.props.children, predicate)];
}
const types = loadModule('features/topics/types/topic.types.ts');
const ui = Object.fromEntries(
  [
    'Button',
    'Dialog',
    'Select',
    'Input',
    'StatisticsBar',
    'TablePagination',
    'TableSkeleton',
    'Skeleton',
    'TopicDetailSkeleton',
    'Checkbox',
    'InfoCell',
    'Card',
    'CardHeader',
    'CardTitle',
    'TopicStatusBadge',
    'TopicPreview',
    'TopicProposalSchedule',
    'TopicReviewDialog',
    'TopicReviewTable',
  ].map((name) => [name, { [name]: name }]),
);
const common = { 'react/jsx-runtime': jsxRuntime, 'lucide-react': icons };

test('review table only allows selecting approved topics from one batch', () => {
  const { TopicReviewTable } = loadModule('features/topics/components/TopicReviewTable.tsx', {
    ...common,
    '@/components/ui/button': ui.Button,
    '@/components/ui/checkbox': ui.Checkbox,
    '@/components/ui/info-cell': ui.InfoCell,
    '@/components/ui/skeleton': ui.Skeleton,
    './TopicStatusBadge': ui.TopicStatusBadge,
  });
  const topics = [
    fullTopic,
    { ...fullTopic, id: 'approved-1', status: 'Approved' },
    { ...fullTopic, id: 'approved-2', status: 'Approved', batchId: 'batch-2' },
  ];
  let selected;
  const render = (selection) =>
    TopicReviewTable({
      topics,
      batches: [],
      advisorNames: {},
      selected: selection,
      pageNumber: 2,
      pageSize: 20,
      busy: false,
      onSelect: (items) => {
        selected = items;
      },
      onOpen() {},
    });
  let checkboxes = walk(render([]), (node) => node.type === 'Checkbox');
  assert.equal(checkboxes[0].props.disabled, true); // Mixed batches: no ambiguous select-all.
  assert.equal(checkboxes[1].props.disabled, true); // Submitted.
  checkboxes[2].props.onCheckedChange(true);
  checkboxes = walk(render(selected), (node) => node.type === 'Checkbox');
  assert.equal(checkboxes[3].props.disabled, true); // A different batch.
  assert.equal(checkboxes[0].props.disabled, false);
  checkboxes[0].props.onCheckedChange(false);
  assert.deepEqual(selected, []);
});

test('review names stay skeletons until their own queries finish and cached names never flash back to IDs', () => {
  const { TopicReviewTable } = loadModule('features/topics/components/TopicReviewTable.tsx', {
    ...common,
    '@/components/ui/button': ui.Button,
    '@/components/ui/checkbox': ui.Checkbox,
    '@/components/ui/info-cell': ui.InfoCell,
    '@/components/ui/skeleton': ui.Skeleton,
    './TopicStatusBadge': ui.TopicStatusBadge,
  });
  const props = {
    topics: [fullTopic],
    batches: [],
    advisorNames: {},
    selected: [],
    pageNumber: 1,
    pageSize: 20,
    busy: false,
    onSelect() {},
    onOpen() {},
  };
  const cells = (overrides) =>
    walk(TopicReviewTable({ ...props, ...overrides }), (node) => node.type === 'td');
  const loading = cells({ advisorsLoading: true, batchesLoading: true });
  assert.equal(walk(loading[3], (node) => node.type === 'Skeleton').length, 1);
  assert.equal(walk(loading[4], (node) => node.type === 'Skeleton').length, 1);
  assert.equal(walk(loading[3], (node) => node.type === 'span').length, 0); // No temporary ID.
  const resolved = cells({
    advisorsLoading: true,
    batchesLoading: true,
    advisorNames: { 'lecturer-1': 'Nguyễn Văn An' },
    batches: [{ id: 'batch-1', batchName: 'Đợt 1' }],
  });
  assert.equal(resolved[3].props.children, 'Nguyễn Văn An');
  assert.equal(resolved[4].props.children, 'Đợt 1');
  const failedOrForbidden = cells({ advisorsLoading: false, batchesLoading: false });
  assert.equal(walk(failedOrForbidden[3], (node) => node.type === 'Skeleton').length, 0);
  assert.equal(
    walk(failedOrForbidden[3], (node) => node.type === 'span')[0].props.title,
    'lecturer-1',
  );
});

test('proposal batch cells and shared preview keep skeletons while related names are unresolved', () => {
  const { TopicsTable } = loadModule('features/topics/components/TopicsTable.tsx', {
    ...common,
    '@/components/ui/button': ui.Button,
    '@/components/ui/info-cell': ui.InfoCell,
    '@/components/ui/skeleton': ui.Skeleton,
    '../types/topic.types': types,
    './TopicStatusBadge': ui.TopicStatusBadge,
  });
  const props = {
    topics: [fullTopic],
    batches: [],
    pageNumber: 1,
    pageSize: 20,
    busy: false,
    onOpen() {},
    onCopy() {},
    onSubmit() {},
  };
  let table = TopicsTable({ ...props, batchesLoading: true });
  assert.equal(walk(table, (node) => node.type === 'Skeleton').length, 1);
  table = TopicsTable({
    ...props,
    batchesLoading: true,
    batches: [{ id: 'batch-1', batchName: 'Đợt 1' }],
  });
  assert.equal(walk(table, (node) => node.type === 'Skeleton').length, 0);
  const { TopicPreview } = loadModule('features/topics/components/TopicPreview.tsx', {
    './TopicReviewNote': { TopicReviewNote: 'TopicReviewNote' },
    ...common,
    '@/components/ui/badge': { Badge: 'Badge' },
    '@/components/ui/skeleton': ui.Skeleton,
  });
  const preview = TopicPreview({
    values: fullTopic,
    advisorName: 'Mã GVHD: lecturer-1',
    batchName: 'Mã đợt: batch-1',
    advisorLoading: true,
    batchLoading: true,
  });
  const fields = walk(preview, (node) => node.type === 'dd');
  assert.equal(fields.length, 2);
  assert.ok(
    fields.every(
      (field) =>
        field.props['aria-busy'] && walk(field, (node) => node.type === 'Skeleton').length === 1,
    ),
  );
});

function dialogFixture(status = 'Submitted') {
  const state = [];
  let cursor = 0;
  const actions = [];
  const { TopicReviewDialog } = loadModule('features/topics/components/TopicReviewDialog.tsx', {
    ...common,
    react: {
      useState: (initial) => {
        const index = cursor++;
        if (!(index in state)) state[index] = initial;
        return [
          state[index],
          (value) => {
            state[index] = value;
          },
        ];
      },
    },
    '@tanstack/react-query': { useQuery: () => ({ data: { ...fullTopic, status } }) },
    '@/components/ui/dialog': ui.Dialog,
    '@/components/ui/button': ui.Button,
    '@/components/ui/select': ui.Select,
    '@/shared/auth': { useAuth: () => ({ user: { id: 'faculty' } }) },
    '../services/topicApi': { topicApi: {}, getTopicErrorMessage: String },
    '../services/topicReviewApi': { topicReviewApi: {} },
    '../types/topic.types': types,
    './TopicPreview': ui.TopicPreview,
    './TopicStatusBadge': ui.TopicStatusBadge,
    './TopicDetailSkeleton': ui.TopicDetailSkeleton,
  });
  return {
    actions,
    setStatus(value) {
      status = value;
    },
    render() {
      cursor = 0;
      return TopicReviewDialog({
        topicId: 'topic-1',
        batches: [],
        advisorNames: {},
        busy: false,
        error: '',
        onClose() {},
        onAction: (action) => actions.push(action),
      });
    },
  };
}

test('review requires an explicit decision and confirmation; approval never publishes automatically', () => {
  const dialog = dialogFixture();
  let tree = dialog.render();
  const button = (label) =>
    walk(tree, (node) => node.type === 'Button' && node.props.children === label)[0];
  assert.equal(button('Gửi kết luận').props.disabled, true);
  walk(tree, (node) => node.type === 'Select')[0].props.onChange('Approved');
  tree = dialog.render();
  button('Gửi kết luận').props.onClick();
  assert.equal(dialog.actions.length, 0);
  tree = dialog.render();
  assert.equal(walk(tree, (node) => node.props['aria-label'] === 'Xác nhận thao tác').length, 1);
  button('Xác nhận').props.onClick();
  assert.deepEqual(dialog.actions, [
    { kind: 'review', id: 'topic-1', decision: 'Approved', reviewNote: null },
  ]);
});

test('approved detail publishes only itself; other lifecycle states have no decision action', () => {
  const dialog = dialogFixture('Approved');
  let tree = dialog.render();
  walk(
    tree,
    (node) => node.type === 'Button' && node.props.children === 'Công bố đề tài',
  )[0].props.onClick();
  tree = dialog.render();
  walk(
    tree,
    (node) => node.type === 'Button' && node.props.children === 'Xác nhận',
  )[0].props.onClick();
  assert.deepEqual(dialog.actions, [
    { kind: 'publish', batchId: 'batch-1', topicIds: ['topic-1'] },
  ]);
  for (const status of ['Draft', 'NeedRevision', 'Rejected', 'Published', 'Closed', 'Archived']) {
    const readonly = dialogFixture(status).render();
    assert.equal(walk(readonly, (node) => node.type === 'Select').length, 0);
    assert.equal(
      walk(readonly, (node) => node.type === 'Button' && node.props.children !== 'Đóng').length,
      0,
    );
  }
});

test('revision feedback is required, preserved on back and sent in the confirmed action', () => {
  const dialog = dialogFixture();
  let tree = dialog.render();
  const button = (label) =>
    walk(tree, (node) => node.type === 'Button' && node.props.children === label)[0];
  walk(tree, (node) => node.type === 'Select')[0].props.onChange('NeedRevision');
  tree = dialog.render();
  button('Gửi kết luận').props.onClick();
  tree = dialog.render();
  assert.equal(walk(tree, (node) => node.props.role === 'alert').length, 1);
  assert.equal(dialog.actions.length, 0);
  const note = '  Bổ sung dữ liệu thử nghiệm.\nLàm rõ tiêu chí đánh giá.  ';
  walk(tree, (node) => node.type === 'textarea')[0].props.onChange({ target: { value: note } });
  tree = dialog.render();
  button('Gửi kết luận').props.onClick();
  tree = dialog.render();
  assert.equal(walk(tree, (node) => node.type === 'textarea')[0].props.disabled, true);
  button('Quay lại').props.onClick();
  tree = dialog.render();
  assert.equal(walk(tree, (node) => node.type === 'textarea')[0].props.value, note);
  button('Gửi kết luận').props.onClick();
  tree = dialog.render();
  button('Xác nhận').props.onClick();
  assert.deepEqual(dialog.actions, [
    { kind: 'review', id: 'topic-1', decision: 'NeedRevision', reviewNote: note.trim() },
  ]);
});

test('feedback display handles null, preserves line breaks and renders content as text', () => {
  const { TopicReviewNote } = loadModule('features/topics/components/TopicReviewNote.tsx', {
    'react/jsx-runtime': jsxRuntime,
  });
  assert.equal(TopicReviewNote({ note: null }), null);
  assert.equal(TopicReviewNote({ note: '  ' }), null);
  const note = 'Dòng một\n<script>Ví dụ nội dung</script>';
  const tree = TopicReviewNote({ note });
  assert.equal(walk(tree, (node) => node.type === 'p')[0].props.children, note);
  assert.match(walk(tree, (node) => node.type === 'p')[0].props.className, /whitespace-pre-wrap/);
  assert.equal(walk(tree, (node) => node.type === 'script').length, 0);
});

test('a refreshed approval cannot turn an existing review confirmation into a publish command', () => {
  const dialog = dialogFixture();
  let tree = dialog.render();
  walk(tree, (node) => node.type === 'Select')[0].props.onChange('Approved');
  tree = dialog.render();
  walk(
    tree,
    (node) => node.type === 'Button' && node.props.children === 'Gửi kết luận',
  )[0].props.onClick();
  dialog.setStatus('Approved');
  tree = dialog.render();
  assert.equal(
    walk(tree, (node) => node.type === 'Button' && node.props.children === 'Xác nhận').length,
    0,
  );
  walk(
    tree,
    (node) => node.type === 'Button' && node.props.children === 'Công bố đề tài',
  )[0].props.onClick();
  assert.equal(dialog.actions.length, 0); // Publication still requires its own confirmation.
});

function pageFixture(roles = ['Faculty'], queryState = {}) {
  const state = [];
  let cursor = 0;
  let queries;
  let tree;
  const mutationCalls = [];
  const invalidations = [];
  let mutationOptions;
  const { TopicReviewPage } = loadModule('features/topics/pages/TopicReviewPage.tsx', {
    ...common,
    react: {
      useState: (initial) => {
        const index = cursor++;
        if (!(index in state)) state[index] = initial;
        return [
          state[index],
          (value) => {
            state[index] = value;
          },
        ];
      },
    },
    '@tanstack/react-query': {
      useQueryClient: () => ({
        invalidateQueries: async (options) => invalidations.push(options.queryKey),
      }),
      useQuery: (options) => {
        queries.push(options);
        return {
          data: options.queryKey.includes('list')
            ? { items: [fullTopic], pageNumber: options.queryKey[4], pageSize: 20, totalCount: 50 }
            : {},
          ...queryState,
        };
      },
      useMutation: (options) => {
        mutationOptions = options;
        return { mutate: (action) => mutationCalls.push(action), reset() {} };
      },
    },
    ...Object.fromEntries(
      [
        'Button',
        'Dialog',
        'Input',
        'Select',
        'StatisticsBar',
        'TablePagination',
        'TableSkeleton',
        'Skeleton',
      ].map((name) => [
        `@/components/ui/${name.replace(/[A-Z]/g, (c, i) => `${i ? '-' : ''}${c.toLowerCase()}`)}`,
        ui[name],
      ]),
    ),
    '@/components/ui/list-card-header': { ListCardHeader: 'ListCardHeader' },
    '@/components/ui/page-header': { PageHeader: 'PageHeader' },
    '@/components/ui/card': { Card: 'Card', CardHeader: 'CardHeader', CardTitle: 'CardTitle' },
    '@/shared/auth': {
      useAuth: () => ({
        user: { id: 'faculty' },
        hasRole: (required) => required.some((role) => roles.includes(role)),
        hasPermission: () => false,
      }),
    },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({ data: [{ id: 'batch-1', batchName: 'Đợt 1' }] }),
    },
    '../components/TopicProposalSchedule': ui.TopicProposalSchedule,
    '../components/TopicReviewDialog': ui.TopicReviewDialog,
    '../components/TopicReviewTable': ui.TopicReviewTable,
    '../services/topicApi': { getTopicErrorMessage: String },
    '../services/topicReviewApi': { topicReviewApi: {} },
    '../types/topic.types': types,
  });
  function render() {
    cursor = 0;
    queries = [];
    tree = TopicReviewPage();
  }
  render();
  return {
    render,
    find: (predicate) => walk(tree, predicate),
    query: () => queries[0],
    queries: () => queries,
    mutationCalls,
    invalidations,
    complete: (result, action) => mutationOptions.onSuccess(result, action),
  };
}

test('review refresh retains only the same reviewer data and locks selection/publication', () => {
  const page = pageFixture(['Faculty'], { isFetching: true, isPending: false });
  const query = page.query();
  const previous = { items: [fullTopic] };
  assert.equal(query.placeholderData(previous, { queryKey: query.queryKey }), previous);
  assert.equal(
    query.placeholderData(previous, { queryKey: ['topic-review', 'another-faculty'] }),
    undefined,
  );
  assert.equal(query.placeholderData(previous, undefined), undefined);
  assert.equal(page.find((node) => node.type === 'TopicReviewTable')[0].props.busy, true);
  assert.equal(page.find((node) => node.type === 'TablePagination')[0].props.isLoading, true);
});

test('review queue defaults to Submitted and explicit filters retain the applied batch', () => {
  const page = pageFixture();
  assert.equal(page.query().queryKey[3].status, 'Submitted');
  const statusOptions = page.find(
    (node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài',
  )[0].props.options;
  assert.equal(
    statusOptions.some((option) => option.value === 'Draft'),
    false,
  );
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo đợt KLTN')[0]
    .props.onChange('batch-1');
  page.render();
  page.find((node) => node.type === 'form')[0].props.onSubmit({ preventDefault() {} });
  page.render();
  page
    .find((node) => node.type === 'TopicReviewTable')[0]
    .props.onSelect([{ ...fullTopic, status: 'Approved' }]);
  page.render();
  page.find((node) => node.type === 'TablePagination')[0].props.onPageChange(3);
  page.render();
  const stats = page.find((node) => node.type === 'StatisticsBar')[0].props.items;
  assert.ok(stats.every((item) => typeof item.onClick === 'function'));
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái đề tài')[0]
    .props.onChange('Approved');
  page.render();
  page.find((node) => node.type === 'form')[0].props.onSubmit({ preventDefault() {} });
  page.render();
  assert.deepEqual(page.query().queryKey[3], {
    keyword: '',
    batchId: 'batch-1',
    status: 'Approved',
  });
  assert.equal(page.query().queryKey[4], 1);
  assert.deepEqual(page.find((node) => node.type === 'TopicReviewTable')[0].props.selected, []);
  page
    .find((node) => node.type === 'Button' && node.props.children === 'Bỏ lọc')[0]
    .props.onClick();
  page.render();
  assert.deepEqual(page.query().queryKey[3], { keyword: '', batchId: '', status: '' });
});

test('review statistic clicks filter immediately and clear stale publication selections', () => {
  const page = pageFixture();
  page
    .find((node) => node.type === 'TopicReviewTable')[0]
    .props.onSelect([{ ...fullTopic, status: 'Approved' }]);
  page.render();
  page.find((node) => node.type === 'TablePagination')[0].props.onPageChange(3);
  page.render();
  for (const [key, status] of [
    ['approved', 'Approved'],
    ['published', 'Published'],
    ['submitted', 'Submitted'],
    ['total', ''],
  ]) {
    page
      .find((node) => node.type === 'StatisticsBar')[0]
      .props.items.find((item) => item.key === key)
      .onClick();
    page.render();
    assert.equal(page.query().queryKey[3].status, status);
    assert.equal(page.query().queryKey[4], 1);
    assert.deepEqual(page.find((node) => node.type === 'TopicReviewTable')[0].props.selected, []);
    assert.equal(
      page
        .find((node) => node.type === 'StatisticsBar')[0]
        .props.items.find((item) => item.key === key).active,
      true,
    );
  }
});

test('successful review refreshes both reviewer and lecturer lists; Lecturer/Student cannot issue review actions', async () => {
  const page = pageFixture();
  await page.complete(
    { ...fullTopic, status: 'Approved' },
    { kind: 'review', id: 'topic-1', decision: 'Approved' },
  );
  page.render();
  assert.deepEqual(page.invalidations, [['topic-review'], ['my-proposed-topics']]);
  assert.equal(
    page.find(
      (node) =>
        node.props.role === 'status' && String(node.props.children).includes('chưa được công bố'),
    ).length,
    1,
  );
  for (const roles of [['Lecturer'], ['Student'], ['CouncilMember']]) {
    const denied = pageFixture(roles);
    assert.equal(denied.query().enabled, false);
    assert.equal(denied.queries()[2].enabled, false); // No unauthorized staff lookup.
    denied.find((node) => node.type === 'TopicReviewTable')[0].props.onOpen(fullTopic);
    denied.render();
    denied
      .find((node) => node.type === 'TopicReviewDialog')[0]
      .props.onAction({ kind: 'review', id: 'topic-1', decision: 'Approved' });
    assert.deepEqual(denied.mutationCalls, []);
  }
});
