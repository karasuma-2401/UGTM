import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import axios from 'axios';
import * as icons from 'lucide-react';

let currentQuery;
let queryOptions;
const dependencies = {
  react: React,
  'react/jsx-runtime': jsxRuntime,
  '@tanstack/react-query': {
    useQuery: (options) => {
      queryOptions = options;
      return currentQuery;
    },
    useQueryClient: () => ({ invalidateQueries: () => Promise.resolve() }),
    useMutation: () => ({ isPending: false, isError: false, mutate() {} }),
  },
  axios: { default: axios },
  'lucide-react': icons,
  '@/components/ui/button': {
    Button: ({ children }) => React.createElement('button', null, children),
  },
  '@/shared/auth': { useAuth: () => ({ user: { id: 'current-user' } }) },
  '../services/similarityApi': { similarityApi: {} },
};
const source = readFileSync(
  new URL('../src/features/thesis/components/SimilarityReportPanel.tsx', import.meta.url),
  'utf8',
);
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022,
    jsx: ts.JsxEmit.ReactJSX,
    esModuleInterop: true,
  },
});
const exports = {};
new Function('require', 'exports', outputText)((name) => {
  assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
  return { __esModule: true, ...dependencies[name] };
}, exports);
const { SimilarityReportPanel } = exports;
const render = () =>
  renderToStaticMarkup(
    React.createElement(SimilarityReportPanel, { reportId: 'report-1', versionNo: 2 }),
  );

test('student receipt shows status and filename without exposing detailed matches', () => {
  currentQuery = {
    isPending: false,
    isError: false,
    isFetching: false,
    data: {
      status: 'Completed',
      statusDescription: 'Hoàn tất',
      fileName: 'report.pdf',
      fileSize: 1024,
      checksumSha256: null,
      notice: 'Chỉ xem biên nhận',
      versionNo: 2,
    },
  };
  const html = render();
  assert.match(html, /report\.pdf/);
  assert.match(html, /Chỉ xem biên nhận/);
  assert.doesNotMatch(html, /Các đoạn cần đối chiếu|Yêu cầu quét lại/);
  assert.deepEqual(queryOptions.queryKey, ['similarity-report', 'current-user', 'report-1', 2]);
});

test('authorized detailed report shows evidence and only permits retry in terminal states', () => {
  const report = {
    status: 'Completed',
    statusDescription: 'Hoàn tất',
    canRescan: true,
    summary: { similarityCoverage: 0.185, maxRerankerScore: 0.82 },
    findings: [
      {
        passageGroupId: 'g1',
        submissionPage: 2,
        submissionText: 'Đoạn bài nộp',
        sourceFilename: 'source.pdf',
        sourcePage: 3,
        sourceText: 'Đoạn nguồn',
        rerankerScore: 0.8,
        reviewRequired: true,
      },
    ],
    disclaimer: 'Không tự động kết luận.',
  };
  currentQuery = { isPending: false, isError: false, isFetching: false, data: report };
  let html = render();
  assert.match(html, /18,5%/);
  assert.match(html, /Đoạn bài nộp/);
  assert.match(html, /Đoạn nguồn/);
  assert.match(html, /Yêu cầu quét lại/);
  currentQuery = { ...currentQuery, data: { ...report, status: 'Processing' } };
  html = render();
  assert.doesNotMatch(html, /Yêu cầu quét lại/);
  assert.equal(queryOptions.refetchInterval({ state: { data: currentQuery.data } }), 10000);
});

test('a forbidden response hides any previously cached detailed evidence', () => {
  currentQuery = {
    isPending: false,
    isError: true,
    isFetching: false,
    error: new axios.AxiosError('forbidden', 'ERR_BAD_REQUEST', {}, {}, { status: 403, data: {} }),
    data: { status: 'Completed', findings: [{ submissionText: 'Văn bản mật' }] },
  };
  const html = render();
  assert.match(html, /không có quyền/);
  assert.doesNotMatch(html, /Văn bản mật/);
});
