import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function apiFixture({
  fetch = () => {
    throw new Error('unexpected public request');
  },
  httpClient = {},
} = {}) {
  const source = readFileSync(
    new URL('../src/features/library/libraryApi.ts', import.meta.url),
    'utf8',
  ).replaceAll('import.meta.env.DEV', 'false');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  new Function('require', 'exports', 'fetch', outputText)(
    (name) => {
      if (name === '@/shared/api/config') return { API_BASE_URL: 'https://api.example.test' };
      if (name === '@/shared/api/httpClient') return { httpClient };
      throw new Error(`Unexpected dependency ${name}`);
    },
    exports,
    fetch,
  );
  return exports;
}
test('library URL filters round-trip Vietnamese values and keep stable page/sort', () => {
  const api = apiFixture();
  const query = {
    q: 'dữ liệu',
    year: '2025-2026',
    semester: 'Học kỳ 2',
    department: 'Khoa học Máy tính',
    sort: 'title',
    page: 3,
  };
  assert.deepEqual(api.readSearch(api.searchParams(query)), query);
  assert.equal(api.readSearch(new URLSearchParams('page=-1&sort=invalid')).page, 1);
  assert.equal(api.readSearch(new URLSearchParams('page=NaN&sort=invalid')).sort, 'newest');
});
test('public lookup omits session credentials and passes cancellation without refreshing auth', async () => {
  let request;
  const api = apiFixture({
    fetch: async (...args) => {
      request = args;
      return {
        ok: true,
        json: async () => ({ success: true, data: { items: [], totalCount: 0 } }),
      };
    },
  });
  const controller = new AbortController();
  await api.findTheses(api.readSearch(new URLSearchParams('q=học+máy')), 4, controller.signal);
  assert.ok(request[0].startsWith('https://api.example.test/api/library/theses?'));
  assert.equal(new URL(request[0]).searchParams.get('q'), 'học máy');
  assert.equal(request[1].credentials, 'omit');
  assert.equal(request[1].signal.aborted, false);
  controller.abort();
  assert.equal(request[1].signal.aborted, true);
});
test('catalog failures remain errors instead of being replaced by empty or sample data', async () => {
  const api = apiFixture({ fetch: async () => ({ ok: false, status: 503 }) });
  await assert.rejects(
    api.findTheses(api.readSearch(new URLSearchParams()), 12),
    (error) => error instanceof api.LibraryError && error.status === 503,
  );
});
test('document URLs reject unsafe schemes and non-HTTPS production destinations', async () => {
  for (const url of ['javascript:alert(1)', 'http://storage.example.test/file.pdf']) {
    const api = apiFixture({
      httpClient: { get: async () => ({ data: { success: true, data: { downloadUrl: url } } }) },
    });
    await assert.rejects(api.getThesisDocument('approved-slug'), /Địa chỉ tài liệu không hợp lệ/);
  }
});

test('a stale page bookmark shows recovery instead of claiming the catalog is empty', () => {
  const source = readFileSync(
    new URL('../src/features/library/ThesisSearch.tsx', import.meta.url),
    'utf8',
  );
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  let recovered;
  const dependencies = {
    react: { useState: (initial) => [initial, () => {}] },
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    'react-router-dom': {
      Link: () => null,
      useSearchParams: () => [
        new URLSearchParams('page=2'),
        (params) => {
          recovered = params;
        },
      ],
    },
    '@tanstack/react-query': {
      useQuery: () => ({
        data: { items: [], totalCount: 1, years: [], semesters: [], departments: [] },
        isPending: false,
        isError: false,
      }),
    },
    './libraryApi': apiFixture(),
  };
  const exports = {};
  new Function('require', 'exports', outputText)((name) => dependencies[name], exports);
  const tree = exports.ThesisSearch({});
  function find(node, predicate) {
    if (!React.isValidElement(node)) return undefined;
    if (predicate(node)) return node;
    return React.Children.toArray(node.props.children)
      .map((child) => find(child, predicate))
      .find(Boolean);
  }
  assert.ok(
    find(
      tree,
      (node) => node.type === 'h3' && node.props.children === 'Trang kết quả không còn tồn tại',
    ),
  );
  assert.equal(
    find(
      tree,
      (node) => node.type === 'h3' && node.props.children === 'Kho tri thức đang được cập nhật',
    ),
    undefined,
  );
  const recovery = find(
    tree,
    (node) =>
      node.type === 'button' &&
      React.Children.toArray(node.props.children).includes('Về trang đầu'),
  );
  assert.ok(recovery);
  recovery.props.onClick();
  assert.equal(recovered.get('page'), '1');
});
