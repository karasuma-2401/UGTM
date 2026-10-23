import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import { Save, UserPlus } from 'lucide-react';
import * as icons from 'lucide-react';

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
  assert.match(html, /Tài khoản được khởi tạo với vai trò sinh viên/);
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

function usersPageFixture(accountGroup, queryState = {}) {
  const state = [];
  let cursor = 0;
  let queries = [];
  const component = (name) => ({ [name]: name });
  const { UsersPage } = loadModule('features/admin/pages/UsersPage.tsx', {
    react: {
      useState(initial) {
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
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@tanstack/react-query': {
      useQueryClient: () => ({}),
      useQuery(options) {
        queries.push(options);
        return {
          data: options.queryKey.includes('statistics')
            ? { total: 30, pendingActivation: 4, disabled: 2 }
            : { items: [{}], pageNumber: options.queryKey[4], pageSize: 20 },
          ...queryState,
        };
      },
      useMutation: () => ({}),
    },
    '@/shared/auth': { useAuth: () => ({ user: { id: 'admin' }, hasPermission: () => true }) },
    '@/components/ui/button': component('Button'),
    '@/components/ui/input': component('Input'),
    '@/components/ui/card': { Card: 'Card', CardHeader: 'CardHeader', CardTitle: 'CardTitle' },
    '@/components/ui/select': component('Select'),
    '@/components/ui/page-header': component('PageHeader'),
    '@/components/ui/list-card-header': component('ListCardHeader'),
    '@/components/ui/statistics-bar': component('StatisticsBar'),
    '@/components/ui/table-pagination': component('TablePagination'),
    '@/components/ui/table-skeleton': component('TableSkeleton'),
    '../components/UserFormDialog': component('UserFormDialog'),
    '../components/StudentImportDialog': component('StudentImportDialog'),
    '../components/UsersTable': component('UsersTable'),
    '../components/IdentityNavigation': component('IdentityNavigation'),
    '../types/user.types': loadModule('features/admin/types/user.types.ts'),
    '../constants/systemRoles': loadModule('features/admin/constants/systemRoles.ts'),
    '../services/userApi': {
      userApi: { getUsers: async (query) => query },
      getUserErrorMessage: String,
    },
    '@/features/auth/services/authApi': {},
  });
  let tree;
  function render() {
    cursor = 0;
    queries = [];
    tree = UsersPage({ accountGroup });
  }
  function find(predicate, node) {
    if (Array.isArray(node)) return node.flatMap((item) => find(predicate, item));
    if (!React.isValidElement(node)) return [];
    return [...(predicate(node) ? [node] : []), ...find(predicate, node.props.children)];
  }
  render();
  return {
    render,
    find: (predicate) => find(predicate, tree),
    stat: (key) =>
      find((node) => node.type === 'StatisticsBar', tree)[0].props.items.find(
        (item) => item.key === key,
      ),
    listQuery: () => queries[0].queryFn({}),
    listOptions: () => queries[0],
  };
}

for (const accountGroup of ['Student', 'Staff']) {
  test(`${accountGroup} status filters update the server list while statistics support quick filters`, async () => {
    const page = usersPageFixture(accountGroup);
    assert.equal(page.stat('total').active, true);
    page.find((node) => node.type === 'Input')[0].props.onChange({ target: { value: ' NGUYEN ' } });
    page.render();
    if (accountGroup === 'Staff') {
      page
        .find((node) => node.props['aria-label'] === 'Lọc theo vai trò')[0]
        .props.onChange('Lecturer');
      page.render();
    }
    page.find((node) => node.type === 'form')[0].props.onSubmit({ preventDefault() {} });
    page.render();
    assert.equal((await page.listQuery()).keyword, 'NGUYEN');
    assert.equal(page.stat('total').active, false);
    page.find((node) => node.type === 'TablePagination')[0].props.onPageChange(3);
    page.render();

    for (const [key, status] of [
      ['pending', 'PendingActivation'],
      ['disabled', 'Disabled'],
      ['total', ''],
    ]) {
      assert.equal(typeof page.stat(key).onClick, 'function');
      page.find((node) => node.type === 'Input')[0].props.onChange({ target: { value: '' } });
      page.render();
      if (accountGroup === 'Staff') {
        page.find((node) => node.props['aria-label'] === 'Lọc theo vai trò')[0].props.onChange('');
        page.render();
      }
      page
        .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái')[0]
        .props.onChange(status);
      page.render();
      page.find((node) => node.type === 'form')[0].props.onSubmit({ preventDefault() {} });
      page.render();
      assert.deepEqual(await page.listQuery(), {
        keyword: '',
        role: '',
        status,
        accountGroup,
        pageNumber: 1,
        pageSize: 20,
      });
      assert.equal(page.stat(key).active, true);
      assert.equal(
        page.find((node) => node.props['aria-label'] === 'Lọc theo trạng thái')[0].props.value,
        status,
      );
    }
  });
}

test('Users filter chips and Bỏ lọc synchronize applied filters and pending inputs', async () => {
  const page = usersPageFixture('Staff');
  const clear = () =>
    page.find((node) => node.type === 'Button' && node.props.children === 'Bỏ lọc')[0];
  assert.equal(clear().props.disabled, true);
  page
    .find((node) => node.props['aria-label'] === 'Lọc theo trạng thái')[0]
    .props.onChange('PendingActivation');
  page.render();
  page.find((node) => node.type === 'Input')[0].props.onChange({ target: { value: 'An' } });
  page.render();
  page.find((node) => node.type === 'form')[0].props.onSubmit({ preventDefault() {} });
  page.render();
  page.find((node) => node.props['aria-label'] === 'Bỏ lọc trạng thái')[0].props.onClick();
  page.render();
  assert.equal((await page.listQuery()).keyword, 'An');
  assert.equal((await page.listQuery()).status, '');
  page.find((node) => node.type === 'Input')[0].props.onChange({ target: { value: 'unsent' } });
  page.render();
  clear().props.onClick();
  page.render();
  assert.equal(page.find((node) => node.type === 'Input')[0].props.value, '');
  assert.equal((await page.listQuery()).keyword, '');
  assert.equal(page.find((node) => node.props['aria-label'] === 'Bộ lọc đang áp dụng').length, 0);
  assert.equal(clear().props.disabled, true);
  assert.equal(page.stat('total').active, true);
});

test('Users statistics filter immediately and reset paging for both account groups', async () => {
  for (const group of ['Student', 'Staff']) {
    const page = usersPageFixture(group);
    page.find((node) => node.type === 'TablePagination')[0].props.onPageChange(3);
    page.render();
    page.stat('pending').onClick();
    page.render();
    assert.equal((await page.listQuery()).status, 'PendingActivation');
    assert.equal((await page.listQuery()).pageNumber, 1);
    assert.equal(page.stat('pending').active, true);
    page.stat('total').onClick();
    page.render();
    assert.equal((await page.listQuery()).status, '');
  }
});

test('Users keeps rows only within the same account/group and locks actions while refreshing', () => {
  const page = usersPageFixture('Staff', { isFetching: true, isPending: false });
  const query = page.listOptions();
  const previous = { items: [{ id: 'staff-1' }], totalCount: 1 };
  assert.equal(query.placeholderData(previous, { queryKey: query.queryKey }), previous);
  assert.equal(
    query.placeholderData(previous, { queryKey: ['admin-users', 'other-admin', 'Staff'] }),
    undefined,
  );
  assert.equal(
    query.placeholderData(previous, { queryKey: ['admin-users', 'admin', 'Student'] }),
    undefined,
  );
  assert.equal(query.placeholderData(previous, undefined), undefined);
  assert.equal(page.find((node) => node.type === 'UsersTable')[0].props.isChangingStatus, true);
  assert.equal(page.find((node) => node.type === 'TablePagination')[0].props.isLoading, true);
  assert.ok(
    page.find((node) => node.props['aria-busy'] && node.props.className?.includes('opacity-60'))
      .length,
  );
});
