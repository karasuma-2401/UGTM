import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function load(path, dependencies = {}) {
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
const source = (path) => `features/topics/${path}`;
const helpers = load(source('studentRegistration.ts'));
const published = {
  id: 'topic-1',
  batchId: 'batch-1',
  advisorId: 'advisor-1',
  advisorName: 'Nguyễn Văn An',
  titleVi: 'Đề tài đã công bố',
  titleEn: 'Published thesis',
  description: 'Mục tiêu thật',
  requirements: 'Yêu cầu thật',
  status: 'Published',
  isPublished: true,
  studentNumber: 2,
  confirmedStudentsCount: 0,
  reservedSlots: 0,
  availableSlots: 2,
  createdAt: '2026-10-01T00:00:00Z',
};
const emptyStatus = {
  hasRegistered: false,
  groupId: null,
  topicId: null,
  topicTitleVi: null,
  confirmationStatus: null,
  confirmationExpiresAtUtc: null,
  violationCount: 0,
  isRegistrationBlocked: false,
  blockedAtUtc: null,
};
const result = {
  topicId: published.id,
  batchId: published.batchId,
  groupId: 'group-1',
  isPendingConfirmation: false,
  confirmationExpiresAtUtc: null,
  message: 'Đăng ký thành công.',
};
const timeline = {
  batchId: 'batch-1',
  status: 'Open',
  currentLocalDate: '2026-10-27',
  stages: [
    {
      orderIndex: 2,
      stageCode: 'REGISTRATION_AND_SYLLABUS',
      startDate: '2026-10-20',
      deadlineAtUtc: '2099-11-01T00:00:00Z',
    },
  ],
};
const success = (data) => ({ data: { success: true, data } });
function apiFixture(overrides = {}) {
  const calls = [];
  const api = load(source('services/studentTopicApi.ts'), {
    '../studentRegistration': helpers,
    '@/shared/api': {
      httpClient: {
        get: async (path, config) => {
          calls.push(['GET', path, config]);
          return overrides.get ? overrides.get(path, config) : success(emptyStatus);
        },
        post: async (path, body, config) => {
          calls.push(['POST', path, body, config]);
          return overrides.post
            ? overrides.post(path, body, config)
            : success({ ...result, isPendingConfirmation: Boolean(body.partnerUserCode) });
        },
      },
    },
  }).studentTopicApi;
  return { api, calls, posts: () => calls.filter((call) => call[0] === 'POST') };
}

test('catalog uses only the public endpoint with server search, batch, paging and cancellation', async () => {
  const { api, calls } = apiFixture({ get: () => success({ items: [published], totalCount: 50 }) });
  const signal = new AbortController().signal;
  const page = await api.getTopics(
    { keyword: '  học sâu ', batchId: 'batch-1', pageNumber: 2, pageSize: 10 },
    signal,
  );
  assert.equal(page.items[0].advisorName, published.advisorName);
  assert.deepEqual(calls, [
    [
      'GET',
      '/api/topics/all-topics',
      {
        params: {
          keyword: 'học sâu',
          batchId: 'batch-1',
          pageNumber: 2,
          pageSize: 10,
        },
        signal,
      },
    ],
  ]);
  const invalid = apiFixture({
    get: () => success({ items: [{ ...published, status: 'Draft', isPublished: false }] }),
  });
  await assert.rejects(
    invalid.api.getTopics({ keyword: '', batchId: '', pageNumber: 1, pageSize: 10 }),
    /chưa được công bố/,
  );
});

test('solo registration refreshes personal status before posting only topicId', async () => {
  const { api, calls } = apiFixture();
  await api.register({ topic: published, partnerUserCode: '' }, '24521603');
  assert.equal(calls[0][1], '/api/thesis/topic-registrations/my-status');
  assert.equal(calls[1][1], '/api/thesis/topic-registrations/');
  assert.deepEqual(calls[1][2], { topicId: 'topic-1' });
});

test('pair registration sends only the invited MSSV, never a user-supplied registrant or confirmation', async () => {
  const { api, posts } = apiFixture();
  const response = await api.register({ topic: published, partnerUserCode: ' sv02 ' }, 'SV01');
  assert.deepEqual(posts()[0][2], { topicId: published.id, partnerUserCode: 'SV02' });
  assert.equal(posts().length, 1);
  assert.equal(response.isPendingConfirmation, true);
});

test('self-invite, one-person quota, zero available slots, and blocked/stale personal status stop a POST', async () => {
  const first = apiFixture();
  await assert.rejects(
    first.api.register({ topic: published, partnerUserCode: 'SV01' }, ' sv01 '),
    /chính mình/,
  );
  await assert.rejects(
    first.api.register({ topic: { ...published, studentNumber: 1 }, partnerUserCode: 'SV02' }),
    /một sinh viên/,
  );
  await assert.rejects(
    first.api.register({ topic: { ...published, availableSlots: 0 }, partnerUserCode: '' }),
    /đủ chỗ/,
  );
  assert.equal(first.posts().length, 0);
  for (const status of [
    { ...emptyStatus, isRegistrationBlocked: true },
    { ...emptyStatus, hasRegistered: true, topicId: published.id, confirmationStatus: 'Confirmed' },
    { ...emptyStatus, hasRegistered: true, topicId: 'other', batchId: published.batchId },
  ]) {
    const fixture = apiFixture({ get: () => success(status) });
    await assert.rejects(fixture.api.register({ topic: published, partnerUserCode: '' }));
    assert.equal(fixture.posts().length, 0);
  }
});

test('unknown registration batch never blocks all other academic periods; backend remains authoritative', async () => {
  const fixture = apiFixture({
    get: () => success({ ...emptyStatus, hasRegistered: true, topicId: 'older-topic' }),
  });
  await fixture.api.register({ topic: published, partnerUserCode: '' });
  assert.equal(fixture.posts().length, 1);
});

test('a failed preflight and cancellation before POST never create a registration', async () => {
  const failed = apiFixture({
    get: () => {
      throw new Error('Mất kết nối');
    },
  });
  await assert.rejects(
    failed.api.register({ topic: published, partnerUserCode: '' }),
    /Mất kết nối/,
  );
  assert.equal(failed.posts().length, 0);
  const controller = new AbortController();
  const aborted = apiFixture({
    get: () => {
      controller.abort();
      return success(emptyStatus);
    },
  });
  await assert.rejects(
    aborted.api.register({ topic: published, partnerUserCode: '' }, null, controller.signal),
    { name: 'AbortError' },
  );
  assert.equal(aborted.posts().length, 0);
});

test('registration conflicts and confirmation expiry preserve backend messages without retries', async () => {
  for (const message of [
    'Đề tài đã đủ chỉ tiêu.',
    'Bạn không có quyền xác nhận.',
    'Yêu cầu đã hết hạn.',
  ]) {
    const fixture = apiFixture({
      post: () => ({ data: { success: false, error: { code: 'Conflict', message } } }),
    });
    await assert.rejects(
      fixture.api.register({ topic: published, partnerUserCode: '' }),
      (error) => error.message === message,
    );
    assert.equal(fixture.posts().length, 1);
  }
  const confirm = apiFixture();
  await assert.rejects(confirm.api.confirmParticipation('  '), /nhập mã/);
  assert.equal(confirm.posts().length, 0);
  await confirm.api.confirmParticipation(' token-from-mail ');
  assert.deepEqual(confirm.posts()[0].slice(1, 3), [
    '/api/thesis/topic-registrations/confirm',
    { token: 'token-from-mail' },
  ]);
});

test('registration window matches Open, stage start and inclusive UTC deadline; capacity is not availability', () => {
  assert.equal(
    helpers.registrationWindow(timeline, Date.parse('2026-10-27T10:00:00Z')).state,
    'open',
  );
  assert.equal(helpers.registrationWindow({ ...timeline, status: 'Draft' }).state, 'closed');
  assert.equal(
    helpers.registrationWindow({ ...timeline, currentLocalDate: '2026-10-01' }).state,
    'closed',
  );
  const deadline = Date.parse(timeline.stages[0].deadlineAtUtc);
  assert.equal(helpers.registrationWindow(timeline, deadline).state, 'open');
  assert.equal(helpers.registrationWindow(timeline, deadline + 1).state, 'closed');
  assert.equal(helpers.registrationWindow(undefined).state, 'unknown');
  assert.equal(helpers.validateStudentRegistration(published, ''), '');
  assert.equal(
    helpers.invitationExpired(
      {
        ...emptyStatus,
        confirmationStatus: 'Pending',
        confirmationExpiresAtUtc: '2026-10-27T10:00:00Z',
      },
      Date.parse('2026-10-27T10:00:00Z'),
    ),
    true,
  );
});

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
  assert.ok(node, 'Expected element');
  return node;
}
const button = (tree, label) => find(tree, (n) => n.type === 'Button' && text(n) === label);

function uiFixture(file, exportName, props = {}, options = {}) {
  const state = [],
    refs = [],
    effects = [],
    jobs = [],
    submitted = [],
    invalidations = [];
  let cursor = 0,
    refCursor = 0,
    effectCursor = 0,
    mutations = [],
    queries = [];
  const api = options.api ?? apiFixture().api;
  const controls = { pending: false, ...options };
  const dependencies = {
    react: {
      ...React,
      useState(initial) {
        const i = cursor++;
        if (!(i in state)) state[i] = typeof initial === 'function' ? initial() : initial;
        return [
          state[i],
          (value) => {
            state[i] = typeof value === 'function' ? value(state[i]) : value;
          },
        ];
      },
      useRef(initial) {
        const i = refCursor++;
        return (refs[i] ??= { current: initial });
      },
      useEffect(effect) {
        const i = effectCursor++;
        if (!(i in effects)) effects[i] = effect();
      },
    },
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@tanstack/react-query': {
      useQueryClient: () => ({
        invalidateQueries: ({ queryKey }) => {
          invalidations.push(queryKey);
          return Promise.resolve();
        },
      }),
      useQuery(configuration) {
        queries.push(configuration);
        const key = configuration.queryKey[0];
        let data =
          key === 'student-registration-timeline'
            ? (controls.timeline ?? timeline)
            : key === 'student-registration'
              ? (controls.status ?? emptyStatus)
              : key === 'student-topics-total'
                ? { totalCount: 40 }
                : {
                    items: [published],
                    totalCount: 40,
                    pageNumber: configuration.queryKey[3],
                    pageSize: configuration.queryKey[4],
                  };
        return {
          data,
          isPending: false,
          isFetching: false,
          isError: false,
          refetch() {},
          ...controls.queryState?.[key],
        };
      },
      useMutation(configuration) {
        mutations.push(configuration);
        return {
          isPending: controls.pending,
          error: controls.error,
          reset() {
            controls.error = undefined;
          },
          mutate(variables) {
            submitted.push(variables);
            jobs.push(
              Promise.resolve()
                .then(() => configuration.mutationFn(variables))
                .then(
                  (result) => configuration.onSuccess?.(result, variables),
                  (error) => {
                    controls.error = error;
                    return configuration.onError?.(error, variables);
                  },
                )
                .finally(() => configuration.onSettled?.()),
            );
          },
        };
      },
    },
    '@/shared/auth': {
      useAuth: () => ({
        user: {
          id: controls.userId ?? 'student-1',
          fullName: 'Sinh viên thử nghiệm',
          userCode: 'SV01',
        },
        hasRole: (required) =>
          (Array.isArray(required) ? required : [required]).some((role) =>
            (controls.roles ?? (controls.student === false ? [] : ['Student'])).some(
              (actual) => actual.toLowerCase() === role.toLowerCase(),
            ),
          ),
        hasPermission: () => controls.canReadSchedule !== false,
      }),
    },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({
        data: [{ id: 'batch-1', batchName: 'Đợt thật' }],
        isPending: false,
        isError: false,
      }),
    },
    '../studentRegistration': helpers,
    '../services/studentTopicApi': { studentTopicApi: api },
    '../services/topicApi': {
      getTopicErrorMessage: (error) => error?.message ?? 'Không thể kết nối',
    },
    './TopicPreview': { TopicPreview: 'TopicPreview' },
    './TopicCapacityStatus': { TopicCapacityStatus: 'TopicCapacityStatus' },
    '../components/TopicCapacityStatus': { TopicCapacityStatus: 'TopicCapacityStatus' },
  };
  for (const name of [
    'StudentRegistrationStatusCard',
    'StudentTopicRegistrationDialog',
    'StudentParticipationDialog',
  ]) {
    dependencies[`../components/${name}`] = { [name]: name };
  }
  for (const [file, name] of Object.entries({
    button: 'Button',
    dialog: 'Dialog',
    input: 'Input',
    select: 'Select',
    card: 'Card',
    'page-header': 'PageHeader',
    'list-card-header': 'ListCardHeader',
    'statistics-bar': 'StatisticsBar',
    'status-badge': 'StatusBadge',
    'info-cell': 'InfoCell',
    skeleton: 'Skeleton',
    'table-skeleton': 'TableSkeleton',
    'table-pagination': 'TablePagination',
    'selectable-table-row': 'SelectableTableRow',
  }))
    dependencies[`@/components/ui/${file}`] = { [name]: name };
  const component = load(source(file), dependencies)[exportName];
  return {
    render() {
      cursor = 0;
      refCursor = 0;
      effectCursor = 0;
      mutations = [];
      queries = [];
      return component(props);
    },
    settle: () => Promise.all(jobs.splice(0)),
    submitted,
    invalidations,
    controls,
    queries: () => queries,
    mutations: () => mutations,
    unmount: () => effects.forEach((cleanup) => cleanup?.()),
  };
}
const dialog = (props = {}, options = {}) =>
  uiFixture(
    'components/StudentTopicRegistrationDialog.tsx',
    'StudentTopicRegistrationDialog',
    {
      topic: published,
      status: emptyStatus,
      onClose() {},
      onRegistered() {},
      onRefresh() {},
      ...props,
    },
    options,
  );

test('solo dialog requires review, snapshots the request and prevents duplicate POSTs', async () => {
  const api = apiFixture();
  const completed = [];
  const ui = dialog({ onRegistered: (...args) => completed.push(args) }, { api: api.api });
  let tree = ui.render();
  assert.equal(find(tree, (node) => node.type === 'TopicPreview').props.advisorName, published.advisorName);
  assert.equal(api.posts().length, 0);
  button(tree, 'Đăng ký đề tài').props.onClick();
  tree = ui.render();
  assert.match(text(tree), /Đăng ký cá nhân/);
  const confirm = button(tree, 'Xác nhận đăng ký').props.onClick;
  confirm();
  confirm();
  assert.equal(ui.submitted.length, 1);
  await ui.settle();
  assert.equal(api.posts().length, 1);
  assert.equal(completed.length, 1);
  assert.deepEqual(api.posts()[0][2], { topicId: published.id });
  assert.equal(ui.mutations()[0].retry, false);
});

test('pair dialog requires a partner; switching back to solo omits the old invited MSSV', async () => {
  const ui = dialog();
  let tree = ui.render();
  button(tree, '2 sinh viên').props.onClick();
  tree = ui.render();
  button(tree, 'Đăng ký đề tài').props.onClick();
  tree = ui.render();
  assert.match(text(tree), /nhập MSSV/);
  find(tree, (n) => n.props.id === 'registration-partner-code').props.onChange({
    target: { value: 'SV02' },
  });
  tree = ui.render();
  button(tree, 'Đăng ký đề tài').props.onClick();
  tree = ui.render();
  assert.match(text(tree), /SV02/);
  assert.match(text(tree), /24 giờ/);
  button(tree, 'Quay lại').props.onClick();
  tree = ui.render();
  button(tree, 'Cá nhân').props.onClick();
  tree = ui.render();
  button(tree, 'Đăng ký đề tài').props.onClick();
  tree = ui.render();
  button(tree, 'Xác nhận đăng ký').props.onClick();
  assert.equal(ui.submitted[0].partnerUserCode, '');
  await ui.settle();
});

test('closed windows, known full topics and lecturer-assigned registrations remain viewable without registration', () => {
  for (const ui of [
    dialog({}, { timeline: { ...timeline, status: 'Closed' } }),
    dialog({ topic: { ...published, availableSlots: 0 } }),
    dialog({
      status: {
        ...emptyStatus,
        hasRegistered: true,
        topicId: published.id,
        confirmationStatus: 'Confirmed',
      },
    }),
  ]) {
    const tree = ui.render();
    find(tree, (n) => n.type === 'TopicPreview');
    assert.equal(button(tree, 'Đăng ký đề tài').props.disabled, true);
    assert.equal(ui.submitted.length, 0);
  }
});

test('confirmation expiry and conflicts preserve the invitation input for retry and refresh status', async () => {
  const api = apiFixture({
    post: () => ({ data: { success: false, error: { message: 'Yêu cầu đã hết hạn.' } } }),
  });
  let refreshed = 0;
  const ui = uiFixture(
    'components/StudentParticipationDialog.tsx',
    'StudentParticipationDialog',
    {
      onClose() {},
      onConfirmed() {
        assert.fail('Expired token must not succeed');
      },
      onRefresh: () => refreshed++,
    },
    { api: api.api },
  );
  let tree = ui.render();
  assert.equal(api.posts().length, 0); // Viewing never consumes a token.
  find(tree, (n) => n.props.id === 'participation-token').props.onChange({
    target: { value: 'expired-token' },
  });
  tree = ui.render();
  const submit = find(tree, (n) => n.type === 'form').props.onSubmit;
  submit({ preventDefault() {} });
  submit({ preventDefault() {} });
  await ui.settle();
  tree = ui.render();
  assert.equal(api.posts().length, 1);
  assert.equal(refreshed, 1);
  assert.equal(
    find(tree, (n) => n.props.id === 'participation-token').props.value,
    'expired-token',
  );
  assert.match(text(tree), /Yêu cầu đã hết hạn/);
});

test('catalog paging, filtering and scope totals follow server data and reset correctly', () => {
  const ui = uiFixture('pages/StudentTopicCatalogPage.tsx', 'StudentTopicCatalogContent');
  let tree = ui.render();
  const queries = ui.queries();
  assert.deepEqual(queries[0].queryKey, [
    'student-topics',
    'student-1',
    { keyword: '', batchId: '' },
    1,
    10,
  ]);
  const old = { items: [published] };
  assert.equal(
    queries[0].placeholderData(old, {
      queryKey: ['student-topics', 'someone-else', { keyword: '', batchId: '' }],
    }),
    undefined,
  );
  find(tree, (n) => n.type === 'TablePagination').props.onPageChange(2);
  tree = ui.render();
  find(tree, (n) => n.props['aria-label'] === 'Tìm tên đề tài').props.onChange({
    target: { value: ' học sâu ' },
  });
  tree = ui.render();
  find(tree, (n) => n.type === 'form').props.onSubmit({ preventDefault() {} });
  tree = ui.render();
  assert.equal(ui.queries()[0].queryKey[3], 1);
  assert.equal(ui.queries()[0].queryKey[2].keyword, 'học sâu');
  assert.equal(ui.queries()[1].queryKey[0], 'student-topics-total');
  find(tree, (n) => n.props.id === 'student-topic-page-size').props.onChange({
    target: { value: '20' },
  });
  tree = ui.render();
  assert.equal(find(tree, (n) => n.type === 'TablePagination').props.pageSize, 20);
  assert.equal(
    find(tree, (node) => node.type === 'TopicCapacityStatus').props.topic.availableSlots,
    2,
  );
});

test('success refreshes related data and displays an invitation receipt without making A wait for B', () => {
  const ui = uiFixture('pages/StudentTopicCatalogPage.tsx', 'StudentTopicCatalogContent');
  let tree = ui.render();
  button(tree, 'Chi tiết').props.onClick({ stopPropagation() {} });
  tree = ui.render();
  find(tree, (n) => n.type === 'StudentTopicRegistrationDialog').props.onRegistered(
    { ...result, isPendingConfirmation: true, confirmationExpiresAtUtc: '2099-10-28T00:00:00Z' },
    { topic: published, partnerUserCode: 'SV02' },
  );
  tree = ui.render();
  assert.equal(
    nodes(tree).some((n) => n.type === 'StudentTopicRegistrationDialog'),
    false,
  );
  const card = find(tree, (n) => n.type === 'StudentRegistrationStatusCard');
  assert.equal(card.props.receipt.partnerUserCode, 'SV02');
  assert.ok(ui.invalidations.some((key) => key[0] === 'student-registration'));
  assert.ok(ui.invalidations.some((key) => key[0] === 'topic-advising'));
});

test('personal registration states distinguish assigned/confirmed, pending, expired and unknown data', () => {
  function render(status) {
    return uiFixture(
      'components/StudentRegistrationStatusCard.tsx',
      'StudentRegistrationStatusCard',
      {
        status,
        loading: false,
        error: '',
        busy: false,
        receipt: null,
        onRetry() {},
        onConfirm() {},
      },
    ).render();
  }
  assert.match(
    text(render({ ...emptyStatus, hasRegistered: true, confirmationStatus: 'Confirmed' })),
    /không cần đăng ký lại/,
  );
  assert.match(
    text(
      render({
        ...emptyStatus,
        hasRegistered: true,
        confirmationStatus: 'Pending',
        confirmationExpiresAtUtc: '2099-01-01T00:00:00Z',
      }),
    ),
    /Chờ bạn xác nhận/,
  );
  const expired = render({
    ...emptyStatus,
    hasRegistered: true,
    confirmationStatus: 'Pending',
    confirmationExpiresAtUtc: '2000-01-01T00:00:00Z',
  });
  assert.match(text(expired), /Hết hạn xác nhận/);
  assert.equal(
    nodes(expired).some((n) => n.type === 'Button' && text(n) === 'Xác nhận tham gia'),
    false,
  );
});

test('catalog route and sidebar allow Student and Admin; account-owned UI remounts by identity', () => {
  const config = load('shared/layout/navigationConfig.ts', {
    'lucide-react': icons,
  }).navigationSections;
  const entry = config
    .flatMap((section) => section.items)
    .find((item) => item.path === '/topics/catalog');
  assert.deepEqual(entry.roles, ['Student', 'Admin']);
  const routerSource = readFileSync(new URL('../src/app/router.tsx', import.meta.url), 'utf8');
  const dependencies = { 'react/jsx-runtime': jsxRuntime };
  for (const [, names, path] of routerSource.matchAll(/import\s*\{([^}]+)\}\s*from\s*'([^']+)'/g)) {
    dependencies[path] = Object.fromEntries(
      names.split(',').map((name) => [name.trim(), name.trim()]),
    );
  }
  dependencies['react-router-dom'].createBrowserRouter = (routes) => routes;
  const routes = load('app/router.tsx', dependencies).router;
  const route = routes
    .find((r) => r.path === '/')
    .children.find((r) => r.path === 'topics/catalog');
  assert.deepEqual(route.element.props.allowedRoles, ['Student', 'Admin']);
  const ui = uiFixture(
    'pages/StudentTopicCatalogPage.tsx',
    'StudentTopicCatalogPage',
    {},
    { userId: 'student-owner' },
  );
  assert.equal(ui.render().key, 'student-owner');
});

test('Admin can browse published topics without querying student status or showing registration actions', () => {
  const ui = uiFixture(
    'pages/StudentTopicCatalogPage.tsx',
    'StudentTopicCatalogContent',
    {},
    {
      roles: ['Admin'],
      queryState: { 'student-registration': { data: undefined, isPending: true } },
    },
  );
  const tree = ui.render();
  assert.equal(ui.queries()[0].enabled, true);
  assert.equal(ui.queries()[1].enabled, true);
  assert.equal(ui.queries()[2].enabled, false);
  assert.equal(
    nodes(tree).some((node) => node.type === 'StudentRegistrationStatusCard'),
    false,
  );
  assert.equal(
    nodes(tree).some((node) => node.type === 'Button' && text(node) === 'Xác nhận lời mời'),
    false,
  );
  assert.equal(find(tree, (node) => node.type === 'StatisticsBar').props.loading, false);
  const readOnly = dialog({}, { roles: ['Admin'] });
  const detail = readOnly.render();
  find(detail, (node) => node.type === 'TopicPreview');
  assert.equal(
    nodes(detail).some((node) => node.type === 'Button' && text(node).includes('Đăng ký')),
    false,
  );
  assert.equal(readOnly.queries()[0].enabled, false);
  assert.equal(readOnly.submitted.length, 0);
});

test('missing availability never permits a blind registration or treats quota as vacant slots', async () => {
  const unknown = {
    ...published,
    availableSlots: undefined,
    confirmedStudentsCount: undefined,
    reservedSlots: undefined,
  };
  const api = apiFixture();
  await assert.rejects(
    api.api.register({ topic: unknown, partnerUserCode: '' }),
    /Chưa có số liệu chỗ trống/,
  );
  assert.equal(api.posts().length, 0);
  const tree = dialog({ topic: unknown }).render();
  assert.equal(button(tree, 'Đăng ký đề tài').props.disabled, true);
});

test('capacity display differentiates confirmed, reserved and available slots', () => {
  const { TopicCapacityStatus } = load(source('components/TopicCapacityStatus.tsx'), {
    'react/jsx-runtime': jsxRuntime,
    '@/components/ui/status-badge': { StatusBadge: 'StatusBadge' },
  });
  const available = TopicCapacityStatus({
    topic: { ...published, confirmedStudentsCount: 1, availableSlots: 1 },
  });
  assert.match(text(available).replace(/\s+/g, ''), /1\/2SVđãđăngký/);
  assert.match(text(available), /Còn 1 chỗ/);
  const held = TopicCapacityStatus({
    topic: { ...published, confirmedStudentsCount: 1, reservedSlots: 1, availableSlots: 0 },
  });
  assert.match(text(held).replace(/\s+/g, ''), /1\/2SVđãđăngký/);
  assert.match(text(held), /Tạm hết chỗ/);
  assert.match(text(held).replace(/\s+/g, ' '), /1 chỗ chờ xác nhận/);
  const full = TopicCapacityStatus({
    topic: { ...published, confirmedStudentsCount: 2, availableSlots: 0 },
  });
  assert.match(text(full).replace(/\s+/g, ''), /2\/2SVđãđăngký/);
  assert.match(text(full), /Đã đủ chỉ tiêu/);
});

test('an availability refresh replaces the selected snapshot and full rows remain viewable', () => {
  const ui = uiFixture(
    'pages/StudentTopicCatalogPage.tsx',
    'StudentTopicCatalogContent',
    {},
    { queryState: {} },
  );
  let tree = ui.render();
  find(tree, (node) => node.type === 'SelectableTableRow').props.onSelect();
  tree = ui.render();
  assert.equal(find(tree, (node) => node.type === 'SelectableTableRow').props.selected, true);
  assert.equal(nodes(tree).some((node) => node.type === 'StudentTopicRegistrationDialog'), false);
  button(tree, 'Chi tiết').props.onClick({ stopPropagation() {} });
  const full = { ...published, confirmedStudentsCount: 2, reservedSlots: 0, availableSlots: 0 };
  ui.controls.queryState['student-topics'] = {
    data: { items: [full], totalCount: 1, pageNumber: 1, pageSize: 10 },
  };
  tree = ui.render();
  assert.equal(
    find(tree, (node) => node.type === 'StudentTopicRegistrationDialog').props.topic.availableSlots,
    0,
  );
  assert.equal(button(tree, 'Chỉ xem').props.disabled, false);
});
