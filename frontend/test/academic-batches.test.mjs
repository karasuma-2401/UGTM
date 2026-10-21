import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
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

const { getCurrentBatches } = loadModule('shared/academic-periods/currentBatches.ts');
const baseBatch = {
  id: 'current',
  batchName: 'Khóa luận thử nghiệm',
  academicYear: '2026-2027',
  semester: 1,
  startDate: '2026-10-01',
  endDate: '2026-11-01',
  timeZoneId: 'Asia/Ho_Chi_Minh',
  status: 'Open',
};
const now = new Date('2026-10-22T05:00:00Z');

test('only Open batches covering today are current; future, expired and closed batches are excluded', () => {
  const batches = [
    baseBatch,
    { ...baseBatch, id: 'draft', status: 'Draft' },
    { ...baseBatch, id: 'closed', status: 'Closed' },
    { ...baseBatch, id: 'archived', status: 'Archived' },
    { ...baseBatch, id: 'past', startDate: '2025-01-01', endDate: '2025-12-01' },
    { ...baseBatch, id: 'future', startDate: '2027-01-01', endDate: '2027-12-01' },
  ];
  assert.deepEqual(getCurrentBatches(batches, now), [baseBatch]);
  assert.deepEqual(getCurrentBatches([], now), []);
});

test('start and end dates are inclusive in each batch timezone, not the browser timezone', () => {
  const boundary = new Date('2026-10-21T17:30:00Z');
  const starting = { ...baseBatch, startDate: '2026-10-22' };
  assert.equal(getCurrentBatches([starting], boundary).length, 1);
  assert.equal(getCurrentBatches([{ ...starting, timeZoneId: 'UTC' }], boundary).length, 0);
  const ending = { ...baseBatch, endDate: '2026-10-22' };
  assert.equal(getCurrentBatches([ending], new Date('2026-10-22T16:59:59Z')).length, 1);
  assert.equal(getCurrentBatches([ending], new Date('2026-10-22T17:00:00Z')).length, 0);
});

test('overlapping current batches are all retained in stable order without mutating API data', () => {
  const later = { ...baseBatch, id: 'later', startDate: '2026-10-15' };
  const batches = [baseBatch, later];
  assert.deepEqual(getCurrentBatches(batches, now), [later, baseBatch]);
  assert.deepEqual(batches, [baseBatch, later]);
  assert.deepEqual(getCurrentBatches([{ ...baseBatch, timeZoneId: 'Invalid/Zone' }], now), []);
});

function apiFixture(get) {
  return loadModule('shared/academic-periods/academicBatchApi.ts', {
    '@/shared/api': { httpClient: { get } },
  }).academicBatchApi;
}

test('shared batch API reads all pages, preserving cancellation and backend values', async () => {
  const requests = [];
  const signal = new AbortController().signal;
  const api = apiFixture(async (path, options) => {
    requests.push([path, options]);
    const page = options.params.pageNumber;
    return {
      data: {
        success: true,
        data: {
          items: [{ ...baseBatch, id: `batch-${page}` }],
          hasNextPage: page === 1,
        },
      },
    };
  });
  const batches = await api.getAll(signal);
  assert.deepEqual(
    batches.map((batch) => batch.id),
    ['batch-1', 'batch-2'],
  );
  assert.equal(batches[0].timeZoneId, baseBatch.timeZoneId);
  assert.deepEqual(
    requests,
    [1, 2].map((pageNumber) => [
      '/api/academic-periods/batches',
      { params: { pageNumber, pageSize: 100 }, signal },
    ]),
  );
});

test('a failed batch request cannot be mistaken for an empty successful list', async () => {
  const api = apiFixture(async () => ({
    data: {
      success: false,
      error: { message: 'Không có quyền đọc đợt KLTN.' },
    },
  }));
  await assert.rejects(api.getAll(), /Không có quyền đọc đợt KLTN/);
});

test('timeline API uses the existing batch endpoint and forwards cancellation', async () => {
  const requests = [];
  const timeline = { batchId: 'batch-1', stages: [] };
  const api = apiFixture(async (...args) => {
    requests.push(args);
    return { data: { success: true, data: timeline } };
  });
  const signal = new AbortController().signal;
  assert.deepEqual(await api.getTimeline('batch-1', signal), timeline);
  assert.deepEqual(requests, [['/api/academic-periods/batches/batch-1/timeline', { signal }]]);
});

test('proposal schedule displays the configured stage and timezone without inventing a deadline', () => {
  let queryOptions;
  let data = {
    batchId: 'batch-1',
    currentLocalDate: '2026-10-03',
    timeZoneId: 'Asia/Ho_Chi_Minh',
    stages: [
      {
        stageCode: 'TOPIC_PREPARATION',
        stageName: 'Chuẩn bị và công bố đề tài',
        startDate: '2026-10-01',
        endDate: '2026-10-07',
        deadlineAtUtc: '2026-10-07T09:00:00Z',
      },
    ],
  };
  const { TopicProposalSchedule } = loadModule(
    'features/topics/components/TopicProposalSchedule.tsx',
    {
      'react/jsx-runtime': jsxRuntime,
      '@tanstack/react-query': {
        useQuery: (options) => {
          queryOptions = options;
          return { data, isPending: false, isError: false };
        },
      },
      'lucide-react': { CalendarDays: () => null },
      '@/components/ui/button': { Button: () => null },
      '@/shared/auth': {
        useAuth: () => ({ user: { id: 'lecturer-1' }, hasPermission: () => true }),
      },
      '@/shared/academic-periods/academicBatchApi': { academicBatchApi: {} },
    },
  );
  const render = () =>
    renderToStaticMarkup(React.createElement(TopicProposalSchedule, { batchId: 'batch-1' }));
  const html = render();
  assert.deepEqual(queryOptions.queryKey, ['academic-batch-timeline', 'lecturer-1', 'batch-1']);
  assert.match(html, /Chuẩn bị và công bố đề tài/);
  assert.match(html, /16:00/);
  assert.match(html, /Đang trong giai đoạn/);
  data = { ...data, stages: [] };
  assert.match(render(), /chưa có lịch chuẩn bị đề tài/);
  assert.doesNotMatch(render(), /16:00/);
});

test('shared query scopes cache by user and only fetches with read permission', async () => {
  let options;
  let user = { id: 'user-1' };
  let permitted = true;
  let forwarded;
  const { useAcademicBatches } = loadModule('shared/academic-periods/useAcademicBatches.ts', {
    '@tanstack/react-query': {
      useQuery: (value) => {
        options = value;
        return value;
      },
    },
    '@/shared/auth': {
      useAuth: () => ({
        user,
        hasPermission: (permission) => {
          assert.equal(permission, 'academic.batches.read');
          return permitted;
        },
      }),
    },
    './academicBatchApi': {
      academicBatchApi: {
        getAll: async (signal) => {
          forwarded = signal;
          return [];
        },
      },
    },
  });
  useAcademicBatches();
  assert.deepEqual(options.queryKey, ['academic-batches', 'user-1']);
  assert.equal(options.enabled, true);
  assert.equal(options.refetchInterval, 60_000);
  const signal = new AbortController().signal;
  await options.queryFn({ signal });
  assert.equal(forwarded, signal);
  user = { id: 'user-2' };
  useAcademicBatches();
  assert.deepEqual(options.queryKey, ['academic-batches', 'user-2']);
  permitted = false;
  useAcademicBatches();
  assert.equal(options.enabled, false);
  user = null;
  useAcademicBatches();
  assert.equal(options.enabled, false);
});

function renderCurrentBatch(query = {}, canRead = true) {
  let retry;
  const { CurrentAcademicBatch } = loadModule('shared/layout/CurrentAcademicBatch.tsx', {
    'react/jsx-runtime': jsxRuntime,
    '@/components/ui/button': {
      Button: ({ children, onClick, ...props }) => {
        retry = onClick;
        return React.createElement('button', props, children);
      },
    },
    '@/shared/auth': { useAuth: () => ({ hasPermission: () => canRead }) },
    '@/shared/academic-periods/currentBatches': { getCurrentBatches },
    '@/shared/academic-periods/useAcademicBatches': {
      useAcademicBatches: () => ({
        data: [],
        isPending: false,
        isError: false,
        isFetching: false,
        refetch() {},
        ...query,
      }),
    },
  });
  return {
    html: renderToStaticMarkup(React.createElement(CurrentAcademicBatch)),
    retry: () => retry?.(),
  };
}

test('sidebar distinguishes loading, empty, missing permission and retryable API failure', () => {
  assert.match(renderCurrentBatch({ isPending: true }).html, /Đang tải thông tin đợt/);
  assert.match(renderCurrentBatch().html, /Chưa có đợt hiện hành/);
  assert.match(renderCurrentBatch({}, false).html, /chưa có quyền xem đợt KLTN/);
  let retries = 0;
  const error = renderCurrentBatch({
    isError: true,
    refetch: () => {
      retries++;
    },
  });
  assert.match(error.html, /Chưa tải được thông tin đợt KLTN/);
  assert.doesNotMatch(error.html, /Chưa có đợt hiện hành/);
  error.retry();
  assert.equal(retries, 1);
});

test('sidebar uses API names/year/semester and displays every current batch without the old mock', () => {
  const ongoing = { ...baseBatch, startDate: '2000-01-01', endDate: '2099-12-31' };
  const { html } = renderCurrentBatch({
    data: [ongoing, { ...ongoing, id: 'second', batchName: 'Đợt bổ sung' }],
  });
  assert.match(html, /Có 2 đợt đang diễn ra/);
  assert.match(html, /Khóa luận thử nghiệm/);
  assert.match(html, /Đợt bổ sung/);
  assert.match(html, /HK1/);
  assert.match(html, /2026-2027/);
  assert.doesNotMatch(html, /2025-2026/);
});
