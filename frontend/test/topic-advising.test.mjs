import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function load(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/features/topics/${path}`, import.meta.url), 'utf8');
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

const advisorId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const batchId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
function topic(index, group = index !== 0) {
  const id = `10000000-0000-0000-0000-${String(index).padStart(12, '0')}`;
  const createdAt = new Date(Date.UTC(2026, 9, 1, index)).toISOString();
  return {
    id,
    batchId,
    advisorId,
    titleVi: `Đề tài ${index}`,
    titleEn: null,
    description: 'Nội dung thực tế',
    requirements: 'Yêu cầu thực tế',
    status: 'Published',
    studentNumber: 2,
    createdAt,
    isPublished: true,
    reviewNote: null,
    registeredStudentsCount: group ? 1 : 0,
    confirmedStudentsCount: group ? 1 : 0,
    pendingConfirmationsCount: index === 2 ? 1 : 0,
    remainingSlots: group ? 1 : 2,
    registrationStatus: group ? 'Registered' : 'Empty',
    hasRegistration: group,
    isAvailable: true,
    group: group
      ? {
          groupId: `20000000-0000-0000-0000-${String(index).padStart(12, '0')}`,
          status: index === 6 ? 'Completed' : index === 3 ? 'InProgress' : 'Registered',
          createdAt,
          students: [
            {
              studentId: `student-${index}`,
              userCode: `2452160${index}`,
              fullName: index === 1 ? 'Nguyễn Minh Đức' : `Sinh viên ${index}`,
              email: `student${index}@gm.uit.edu.vn`,
              phoneNumber: '0900000000',
              joinedAt: createdAt,
            },
          ],
        }
      : null,
  };
}
const data = Array.from({ length: 7 }, (_, i) => topic(i));
const helpers = load('advising.ts');
test('confirmation totals distinguish empty scope, missing API fields and inactive groups', () => {
  assert.deepEqual(helpers.getConfirmationStatistics([]), { pending: 0, confirmed: 0 });
  assert.deepEqual(helpers.getConfirmationStatistics(undefined), { pending: null, confirmed: null });
  assert.deepEqual(helpers.getConfirmationStatistics([
    topic(1),
    { ...topic(2), group: { ...topic(2).group, status: 'Stopped' } },
    topic(6),
  ]), { pending: 0, confirmed: 1 });
  assert.deepEqual(helpers.getConfirmationStatistics([
    { ...topic(1), confirmedStudentsCount: undefined, pendingConfirmationsCount: undefined },
  ]), { pending: null, confirmed: null });
});
const types = load('types/advising.types.ts');
const success = (data) => ({ data: { success: true, data, error: null } });
const service = (get) =>
  load('services/advisingApi.ts', { '@/shared/api': { httpClient: { get } } }).advisingApi;

test('tracking reads every topic page with advisor/batch scope and forwards cancellation', async () => {
  const calls = [];
  const signal = new AbortController().signal;
  const topics = Array.from({ length: 101 }, (_, i) => topic(i));
  const api = service(async (path, config) => {
    calls.push(config.params);
    assert.equal(path, '/api/topics/my-topics');
    assert.equal(config.signal, signal);
    assert.equal(config.params.advisorId, advisorId);
    assert.equal(config.params.batchId, batchId);
    assert.equal(config.params.pageSize, 100);
    const page = config.params.pageNumber;
    return success({
      items: topics.slice((page - 1) * 100, page * 100),
      totalCount: 101,
      hasNextPage: page === 1,
    });
  });
  assert.equal((await api.getTopics(advisorId, batchId, signal)).length, 101);
  assert.deepEqual(
    calls.map((q) => q.pageNumber),
    [1, 2],
  );
});

test('list reads omit an empty batch and never broaden to every advisor when identity is missing', async () => {
  let requests = 0;
  const api = service(async (_path, config) => {
    requests++;
    assert.equal(config.params.batchId, undefined);
    assert.equal(config.params.advisorId, advisorId);
    return success({ items: [], totalCount: 0, hasNextPage: false });
  });
  assert.deepEqual(await api.getTopics(advisorId, ''), []);
  await assert.rejects(api.getTopics('', ''), /giảng viên/);
  assert.equal(requests, 1);
});

test('a failed or incomplete later page is an error, never a partial successful list', async () => {
  const api = service(async (_path, { params }) =>
    params.pageNumber === 1
      ? success({ items: [topic(1)], totalCount: 2, hasNextPage: true })
      : { data: { success: false, error: { message: 'Không đủ quyền đọc nhóm' } } },
  );
  await assert.rejects(api.getTopics(advisorId, ''), /Không đủ quyền đọc nhóm/);
  const incomplete = service(async (_path, { params }) =>
    success({
      items: params.pageNumber === 1 ? [topic(1)] : [],
      totalCount: 2,
      hasNextPage: true,
    }),
  );
  await assert.rejects(incomplete.getTopics(advisorId, ''), /Danh sách đề tài đã thay đổi/);
});

test('details use the tracking endpoint and keep raw unknown confirmation fields unknown', async () => {
  const signal = new AbortController().signal;
  const api = service(async (path, config) => {
    assert.equal(path, `/api/topics/${data[1].id}/tracking`);
    assert.equal(config.signal, signal);
    return success(data[1]);
  });
  const result = await api.getDetail(advisorId, data[1].id, signal);
  assert.equal(result.group.students[0].confirmationStatus, undefined);
  assert.equal(result.group.students[0].confirmedAtUtc, undefined);
});

test('list and details reject data from another advisor, including privileged accounts', async () => {
  const foreign = { ...topic(1), advisorId: 'another-advisor' };
  await assert.rejects(
    service(async () => success({ items: [foreign], totalCount: 1, hasNextPage: false })).getTopics(
      advisorId,
      '',
    ),
    /không thuộc giảng viên/,
  );
  await assert.rejects(
    service(async () => success(foreign)).getDetail(advisorId, foreign.id),
    /không thuộc giảng viên/,
  );
  await assert.rejects(
    service(async () => success(topic(2))).getDetail(advisorId, topic(1).id),
    /không thuộc giảng viên/,
  );
});

test('group scope and local search work across the complete loaded set without confirmation guesses', () => {
  assert.equal(helpers.getAdvisingRows(data, 'registrations', '', '').length, 6);
  assert.equal(helpers.getAdvisingRows(data, 'advising', '', '').length, 5);
  assert.deepEqual(
    helpers.getAdvisingRows(data, 'registrations', 'nguyen duc', '').map((t) => t.id),
    [topic(1).id],
  );
  assert.equal(helpers.getAdvisingRows(data, 'registrations', '24521601', '').length, 1);
  assert.equal(
    helpers.getAdvisingRows(data, 'registrations', 'student1@gm.uit.edu.vn', '').length,
    1,
  );
  assert.equal(
    helpers.getAdvisingRows(data, 'registrations', topic(1).group.groupId, '').length,
    1,
  );
  assert.equal(helpers.getAdvisingRows(data, 'advising', '', 'InProgress').length, 1);
  assert.equal(
    helpers.isAdvisingGroup({ ...topic(1), group: { ...topic(1).group, status: 'Stopped' } }),
    false,
  );
});

function fixture({
  queryOverrides = {},
  detailOverrides = {},
  batchesOverrides = {},
  topics = data,
  pageProps = { embedded: true, initialTab: 'registrations' },
  userId = advisorId,
  canReadBatches = true,
} = {}) {
  const state = [];
  let cursor = 0;
  let queries = [];
  const calls = [];
  const refetch = [];
  const dependencies = {
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
        const detail = options.queryKey[0] === 'topic-advising-detail';
        return {
          data: detail
            ? topics.find((t) => t.id === options.queryKey[2])
            : topics.filter((t) => !options.queryKey[2] || t.batchId === options.queryKey[2]),
          isPending: false,
          isFetching: false,
          isError: false,
          refetch: () => refetch.push(detail ? 'detail' : 'list'),
          ...(detail ? detailOverrides : queryOverrides),
        };
      },
    },
    '@/shared/auth': {
      useAuth: () => ({
        user: { id: userId, fullName: 'Giảng viên' },
        hasPermission: () => canReadBatches,
      }),
    },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({
        data: [{ id: batchId, batchName: 'Đợt thật từ hệ thống' }],
        isPending: false,
        isError: false,
        refetch() {},
        ...batchesOverrides,
      }),
    },
    '../components/TopicStatusBadge': { TopicStatusBadge: 'TopicStatusBadge' },
    '../components/TopicDetailSkeleton': { TopicDetailSkeleton: 'TopicDetailSkeleton' },
    '../types/topic.types': load('types/topic.types.ts'),
    '../types/advising.types': types,
    '../advising': helpers,
    '../services/topicApi': {
      getTopicErrorMessage: (error) => error?.message ?? 'Không thể tải danh sách.',
    },
    '../services/advisingApi': {
      advisingApi: {
        getTopics: async (...args) => {
          calls.push(['list', ...args]);
          return topics;
        },
        getDetail: async (...args) => {
          calls.push(['detail', ...args]);
          return topics.find((t) => t.id === args[1]);
        },
      },
    },
  };
  for (const [file, name] of Object.entries({
    button: 'Button',
    input: 'Input',
    select: 'Select',
    badge: 'Badge',
    'status-badge': 'StatusBadge',
    'selectable-table-row': 'SelectableTableRow',
    card: 'Card',
    'page-header': 'PageHeader',
    'list-card-header': 'ListCardHeader',
    dialog: 'Dialog',
    'statistics-bar': 'StatisticsBar',
    'table-pagination': 'TablePagination',
    'table-skeleton': 'TableSkeleton',
    'info-cell': 'InfoCell',
    skeleton: 'Skeleton',
  }))
    dependencies[`@/components/ui/${file}`] = { [name]: name };
  const { TopicAdvisingPage } = load('pages/TopicAdvisingPage.tsx', dependencies);
  return {
    render() {
      cursor = 0;
      queries = [];
      return TopicAdvisingPage(pageProps);
    },
    queries: () => queries,
    calls,
    refetch,
  };
}

function nodes(tree) {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  return React.isValidElement(tree) ? [tree, ...nodes(tree.props.children)] : [];
}
function text(tree) {
  if (Array.isArray(tree)) return tree.map(text).join(' ');
  if (React.isValidElement(tree)) return text(tree.props.children);
  return typeof tree === 'string' || typeof tree === 'number' ? String(tree) : '';
}
function find(tree, predicate) {
  const node = nodes(tree).find(predicate);
  assert.ok(node, 'Expected UI element was not rendered');
  return node;
}
const rows = (tree) => nodes(tree).filter((n) => n.type === 'SelectableTableRow');
const stats = (tree) => find(tree, (n) => n.type === 'StatisticsBar').props.items;
const button = (tree, label) => find(tree, (n) => n.type === 'Button' && text(n) === label);

test('page uses scoped live queries and confirmation statistics across all active groups', async () => {
  const ui = fixture();
  const tree = ui.render();
  const [list, detail] = ui.queries();
  assert.deepEqual(list.queryKey, ['topic-advising', advisorId, '']);
  assert.equal(list.enabled, true);
  assert.equal(list.refetchInterval, 60_000);
  assert.equal(detail.enabled, false);
  const signal = new AbortController().signal;
  await list.queryFn({ signal });
  assert.deepEqual(ui.calls[0], ['list', advisorId, '', signal]);
  const items = stats(tree);
  assert.equal(items[0].value, 7);
  assert.equal(items[2].value, 5);
  assert.equal(items[1].value, 1);
  assert.equal(items[3].value, 5);
  for (const index of [1, 3]) {
    assert.equal(items[index].onClick, undefined);
  }
  assert.doesNotMatch(text(tree), /Dữ liệu mẫu|Dữ liệu nhóm mẫu/);
  const batch = find(tree, (n) => n.type === 'Select' && n.props['aria-label'] === 'Đợt KLTN');
  assert.equal(batch.props.options[1].value, batchId);
  assert.equal(batch.props.options[1].label, 'Đợt thật từ hệ thống');
});

test('search applies on submit and keeps complete-scope statistics; reset restores groups', () => {
  const ui = fixture();
  let tree = ui.render();
  find(tree, (n) => n.type === 'Input').props.onChange({ target: { value: 'nguyen duc' } });
  tree = ui.render();
  assert.equal(rows(tree).length, 6);
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.equal(rows(tree).length, 1);
  assert.equal(stats(tree)[0].value, 7);
  assert.equal(stats(tree)[2].value, 5);
  button(tree, 'Bỏ lọc').props.onClick();
  assert.equal(rows(ui.render()).length, 6);
});

test('pagination displays all six groups at size ten and changing size resets the page', () => {
  const ui = fixture();
  let tree = ui.render();
  const page = find(tree, (n) => n.type === 'TablePagination').props;
  assert.equal(page.totalCount, 6);
  assert.equal(page.pageSize, 10);
  assert.equal(rows(tree).length, 6);
  const large = fixture({ topics: Array.from({ length: 14 }, (_, i) => topic(i)) });
  tree = large.render();
  find(tree, (n) => n.type === 'TablePagination').props.onPageChange(2);
  tree = large.render();
  assert.equal(rows(tree).length, 3);
  find(tree, (n) => n.props.id === 'advising-page-size').props.onChange({
    target: { value: '20' },
  });
  tree = large.render();
  assert.equal(find(tree, (n) => n.type === 'TablePagination').props.pageNumber, 1);
  assert.equal(rows(tree).length, 13);
});

test('quick filters preserve the actual batch and unsupported demo confirmation filters are discarded', () => {
  const calls = [];
  const ui = fixture({
    pageProps: {
      embedded: true,
      initialTab: 'registrations',
      initialBatchId: batchId,
      initialStatus: 'PendingEmail',
      onQuickFilter: (...args) => calls.push(args),
    },
  });
  const tree = ui.render();
  stats(tree)[0].onClick();
  stats(tree)[2].onClick();
  assert.deepEqual(calls, [
    ['topics', '', batchId],
    ['advising', '', batchId],
  ]);
  const statuses = find(tree, (n) => n.type === 'Select' && n.props['aria-label'] === 'Trạng thái');
  assert.equal(statuses.props.value, '');
  assert.equal(
    statuses.props.options.some((option) =>
      ['PendingEmail', 'Expired', 'Confirmed'].includes(option.value),
    ),
    false,
  );
});

test('details fetch the selected tracking record and never invent confirmation dates or participant roles', async () => {
  const ui = fixture();
  let tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === `Xem chi tiết ${topic(1).titleVi}`).props.onClick();
  tree = ui.render();
  const detail = ui.queries()[1];
  assert.equal(detail.enabled, true);
  assert.deepEqual(detail.queryKey, ['topic-advising-detail', advisorId, topic(1).id]);
  await detail.queryFn({ signal: undefined });
  assert.deepEqual(ui.calls[0], ['detail', advisorId, topic(1).id, undefined]);
  const dialog = find(tree, (n) => n.type === 'Dialog');
  assert.match(text(dialog), /student1@gm.uit.edu.vn/);
  assert.match(text(dialog), /Chưa có trạng thái xác nhận riêng/);
  assert.doesNotMatch(
    text(dialog),
    /Người đăng ký|Đã xác nhận tham gia|Hạn xác nhận tham gia|Đăng ký thành công/,
  );
  button(dialog, 'Liên hệ Khoa').props.onClick();
  tree = ui.render();
  const faculty = find(
    tree,
    (n) => n.type === 'Dialog' && n.props.title === 'Liên hệ Văn phòng Khoa',
  );
  assert.match(text(faculty), /chưa gửi yêu cầu/);
  assert.match(find(faculty, (n) => n.type === 'textarea').props.value, /Nguyễn Minh Đức/);
  assert.doesNotMatch(
    nodes(faculty)
      .filter((n) => n.type === 'Button')
      .map(text)
      .join(' '),
    /Gửi yêu cầu|Hủy nhóm|Từ chối/,
  );
});

test('detail loading and failure do not fall back to list data', () => {
  for (const detailOverrides of [
    { data: undefined, isPending: true },
    { data: topic(1), isError: true, error: new Error('Không có quyền xem') },
  ]) {
    const ui = fixture({ detailOverrides });
    let tree = ui.render();
    find(tree, (n) => n.props['aria-label'] === `Xem chi tiết ${topic(1).titleVi}`).props.onClick();
    tree = ui.render();
    const dialog = find(tree, (n) => n.type === 'Dialog');
    assert.doesNotMatch(text(dialog), /student1@gm.uit.edu.vn/);
    if (detailOverrides.isPending) find(dialog, (n) => n.type === 'TopicDetailSkeleton');
    else {
      assert.match(text(dialog), /Không có quyền xem/);
      button(dialog, 'Thử lại').props.onClick();
      assert.deepEqual(ui.refetch, ['detail']);
    }
  }
});

test('list loading and errors expose retry while hiding stale counts and rows', () => {
  find(
    fixture({ queryOverrides: { data: undefined, isPending: true } }).render(),
    (n) => n.type === 'TableSkeleton',
  );
  const ui = fixture({
    queryOverrides: { data, isError: true, error: new Error('Máy chủ không phản hồi') },
  });
  const tree = ui.render();
  assert.equal(rows(tree).length, 0);
  assert.equal(stats(tree)[0].value, '—');
  assert.equal(stats(tree)[2].value, '—');
  assert.match(text(tree), /Máy chủ không phản hồi/);
  button(tree, 'Thử lại').props.onClick();
  assert.deepEqual(ui.refetch, ['list']);
});

test('row selection stays single and refresh locks row actions', () => {
  const ui = fixture();
  rows(ui.render())[0].props.onSelect();
  assert.equal(rows(ui.render())[0].props.selected, true);
  rows(ui.render())[1].props.onSelect();
  assert.equal(rows(ui.render())[0].props.selected, false);
  assert.equal(rows(ui.render())[1].props.selected, true);
  const refreshing = fixture({ queryOverrides: { isFetching: true } }).render();
  assert.ok(rows(refreshing).every((row) => row.props.disabled));
  assert.ok(
    nodes(refreshing)
      .filter((n) => n.props['aria-label']?.startsWith('Xem chi tiết'))
      .every((n) => n.props.disabled),
  );
});

test('batch names stay skeletons while loading and missing batch permission does not block topic reads', () => {
  const loading = fixture({ batchesOverrides: { data: undefined, isPending: true } }).render();
  assert.ok(nodes(loading).some((n) => n.type === 'Skeleton'));
  const ui = fixture({
    canReadBatches: false,
    batchesOverrides: { data: undefined, isPending: true },
  });
  const tree = ui.render();
  assert.equal(ui.queries()[0].enabled, true);
  assert.equal(
    nodes(tree).some((n) => n.type === 'Skeleton'),
    false,
  );
  assert.match(text(tree), /Mã đợt:/);
});
