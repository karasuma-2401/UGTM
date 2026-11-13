import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

function load(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}

const { parseAuditDiff } = load('features/audit/utils/parseAuditDiff.ts');
let request;
const api = load('features/audit/services/auditLogApi.ts', {
  axios: { default: { isAxiosError: () => false } },
  '@/shared/api': { httpClient: { get: async (url, options) => {
    request = { url, ...options };
    return { data: { success: true, data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 10 } } };
  } } },
});

test('Vietnam calendar day maps to half-open UTC range independent of browser timezone', async () => {
  await api.auditLogApi.getAuditLogs({ fromUtc: '2026-11-12', toUtc: '2026-11-12', searchTerm: 'student', requestId: 'req-1' });
  assert.equal(request.params.fromUtc, '2026-11-11T17:00:00.000Z');
  assert.equal(request.params.toUtc, '2026-11-12T17:00:00.000Z');
  assert.equal(request.params.searchTerm, 'student');
  assert.equal(request.params.requestId, 'req-1');
});

test('rename diff uses recorded title fields, and missing values remain unknown', () => {
  const diff = parseAuditDiff({ action: 'ThesisManagement.RenameRequest.Approved', metadata: { OldTitleVi: 'Cũ', NewTitleVi: 'Mới' } });
  assert.equal(diff.beforeValue, 'Cũ');
  assert.equal(diff.afterValue, 'Mới');
  const missing = parseAuditDiff({ action: 'Identity.User.Created', metadata: null });
  assert.equal(missing.hasDiff, false);
  const update = parseAuditDiff({ action: 'Identity.User.Updated', metadata: { ChangedFields: ['FullName'] } });
  assert.equal(update.hasDiff, false);
});

test('role changes show only recorded grants and removals', () => {
  const diff = parseAuditDiff({ action: 'Identity.User.RolesChanged', metadata: { AddedRoles: ['Admin'], RemovedRoles: ['Student'] } });
  assert.equal(diff.beforeValue, 'Student');
  assert.equal(diff.afterValue, 'Admin');
});

test('auditLogApi forwards sortBy and sortOrder on list queries', async () => {
  await api.auditLogApi.getAuditLogs({ sortBy: 'actor', sortOrder: 'asc', pageNumber: 1, pageSize: 10 });
  assert.equal(request.params.sortBy, 'actor');
  assert.equal(request.params.sortOrder, 'asc');
});
