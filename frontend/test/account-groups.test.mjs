import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import { Save, UserPlus } from 'lucide-react';

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

const groups = loadModule('features/admin/utils/accountGroups.ts');

test('student and staff forms keep distinct role defaults and constraints', () => {
  assert.deepEqual(groups.defaultRolesForGroup('Student'), ['Student']);
  assert.deepEqual(groups.defaultRolesForGroup('Staff'), ['Lecturer']);
  assert.equal(groups.rolesBelongToGroup(['Student'], 'Student'), true);
  assert.equal(groups.rolesBelongToGroup(['Student', 'Admin'], 'Student'), false);
  assert.equal(groups.rolesBelongToGroup(['Student'], 'Staff'), false);
  assert.equal(groups.rolesBelongToGroup([], 'Staff'), false);
  assert.equal(groups.rolesBelongToGroup(['Lecturer', 'CouncilMember'], 'Staff'), true);
});

function renderForm(accountGroup) {
  const { SYSTEM_ROLES } = loadModule('features/admin/constants/systemRoles.ts');
  const { UserForm } = loadModule('features/admin/components/UserForm.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': { Save, UserPlus },
    '../constants/systemRoles': { SYSTEM_ROLES },
    '../utils/accountGroups': groups,
    '@/components/ui/button': {
      Button: ({ children }) => React.createElement('button', null, children),
    },
    '@/components/ui/input': { Input: (props) => React.createElement('input', props) },
    '@/components/ui/checkbox': {
      Checkbox: ({ label, checked }) =>
        React.createElement(
          'label',
          null,
          React.createElement('input', { type: 'checkbox', checked, readOnly: true }),
          label,
        ),
    },
  });
  return renderToStaticMarkup(
    React.createElement(UserForm, {
      accountGroup,
      user: null,
      isSaving: false,
      error: null,
      onSave() {},
      onCancel() {},
    }),
  );
}

test('student form requires MSSV and has no staff role checkboxes', () => {
  const html = renderForm('Student');
  assert.match(html, /Mã số sinh viên/);
  assert.match(html, /Vai trò: Sinh viên/);
  assert.doesNotMatch(html, /type="checkbox"/);
  assert.doesNotMatch(html, /Quản trị viên/);
});

test('staff form offers staff roles and does not allow selecting Student', () => {
  const html = renderForm('Staff');
  assert.match(html, /Mã cán bộ/);
  assert.match(html, /Quản trị viên/);
  assert.equal((html.match(/type="checkbox"/g) || []).length, 5);
  assert.equal((html.match(/checked=""/g) || []).length, 1);
});

function apiFixture(httpClient) {
  return loadModule('features/admin/services/userApi.ts', {
    axios: { default: { isAxiosError: () => false } },
    '@/shared/api': { httpClient },
  }).userApi;
}

test('every statistic uses the selected group and server totals', async () => {
  const requests = [];
  const api = apiFixture({
    get: async (path, config) => {
      requests.push(config.params);
      assert.equal(path, '/api/users');
      const totals = config.params.accountGroup === 'Student' ? [5000, 150, 5] : [20, 2, 1];
      const index =
        config.params.status === 'PendingActivation'
          ? 1
          : config.params.status === 'Disabled'
            ? 2
            : 0;
      return { data: { success: true, data: { totalCount: totals[index], items: [] } } };
    },
  });
  assert.deepEqual(await api.getStatistics('Student'), {
    total: 5000,
    pendingActivation: 150,
    disabled: 5,
  });
  assert.deepEqual(await api.getStatistics('Staff'), {
    total: 20,
    pendingActivation: 2,
    disabled: 1,
  });
  assert.equal(requests.length, 6);
  assert.ok(requests.every((request) => request.pageSize === 1 && request.accountGroup));
});

test('list requests send group, paging and trimmed search to the server', async () => {
  const api = apiFixture({
    get: async (_path, { params }) => {
      assert.equal(params.accountGroup, 'Staff');
      assert.equal(params.keyword, 'GV001');
      assert.equal(params.pageSize, 50);
      assert.equal(params.pageNumber, 2);
      assert.equal(params.role, undefined);
      return { data: { success: true, data: { items: [] } } };
    },
  });
  await api.getUsers({
    accountGroup: 'Staff',
    keyword: ' GV001 ',
    role: '',
    status: '',
    pageNumber: 2,
    pageSize: 50,
  });
});

test('student import uploads multipart data with a fixed Student role', async () => {
  const file = new File(['test'], 'students.xlsx');
  const api = apiFixture({
    post: async (path, body, config) => {
      assert.equal(path, '/api/users/import');
      assert.equal(body.get('role'), 'Student');
      assert.equal(body.get('file').name, 'students.xlsx');
      assert.equal(config.headers['Content-Type'], undefined);
      return { data: { success: true, data: { importedCount: 25 } } };
    },
  });
  assert.equal((await api.importStudents(file)).importedCount, 25);
});
