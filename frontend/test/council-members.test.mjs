import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axios from 'axios';
import ts from 'typescript';

function apiWith(client) {
  const source = readFileSync(
    new URL('../src/features/councils/services/councilMembersApi.ts', import.meta.url),
    'utf8',
  );
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    if (name === 'axios') return { __esModule: true, default: axios };
    if (name === '@/shared/api') return { httpClient: client };
    throw new Error(`Unexpected dependency: ${name}`);
  }, exports);
  return exports.councilMembersApi;
}

test('member API uses candidate and roster endpoints with the expected payloads', async () => {
  const calls = [];
  const response = { data: { success: true, data: [] } };
  const client = Object.fromEntries(
    ['get', 'post', 'put', 'delete'].map((method) => [
      method,
      async (...args) => {
        calls.push([method, ...args]);
        return response;
      },
    ]),
  );
  const api = apiWith(client);
  const signal = new AbortController().signal;
  await api.candidates('c1', ' An ', signal);
  await api.add('c1', { userId: 'u1', role: 'Member' });
  await api.replace('c1', [{ userId: 'u1', role: 'Chairman' }]);
  await api.remove('c1', 'u1');
  await api.submit('c1', '  Đã kiểm tra  ');
  assert.deepEqual(calls, [
    ['get', '/api/councils/c1/member-candidates', { params: { keyword: 'An' }, signal }],
    ['post', '/api/councils/c1/members', { members: [{ userId: 'u1', role: 'Member' }] }],
    ['put', '/api/councils/c1/members', { members: [{ userId: 'u1', role: 'Chairman' }] }],
    ['delete', '/api/councils/c1/members/u1'],
    ['post', '/api/councils/c1/submit', { notes: 'Đã kiểm tra' }],
  ]);
});

test('member API does not treat a rejected server response as success', async () => {
  const api = apiWith({
    post: async () => ({ data: { success: false, error: { message: 'Conflict' } } }),
  });
  await assert.rejects(api.add('c1', { userId: 'u1', role: 'Member' }), /Conflict/);
});
