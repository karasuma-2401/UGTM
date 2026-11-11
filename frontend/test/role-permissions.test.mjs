import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as jsx from 'react/jsx-runtime';

test('permission checkbox labels do not also toggle their containing card', () => {
  const { PermissionCheckboxItem } = load(
    'features/identity/components/role-permissions/PermissionCheckboxItem.tsx',
    { '@/components/ui/checkbox': { Checkbox: 'Checkbox' }, '@/lib/utils': { cn: () => '' } },
  );
  const changes = [];
  const render = (disabled = false) => PermissionCheckboxItem({
    permission: { id: 7, permissionName: 'Cấu hình điểm', permissionCode: 'scores.manage' },
    checked: false,
    disabled,
    onToggle: (...args) => changes.push(args),
  });
  const card = render();
  const checkbox = walk(card, (node) => node.type === 'Checkbox')[0];
  card.props.onClick({ target: { closest: () => ({ tagName: 'LABEL' }) } });
  assert.deepEqual(changes, []);
  checkbox.props.onCheckedChange(true);
  assert.deepEqual(changes, [[7, true]]);
  card.props.onClick({ target: { closest: () => null } });
  assert.deepEqual(changes, [[7, true], [7, true]]);
  render(true).props.onClick({ target: { closest: () => null } });
  assert.equal(changes.length, 2);
});

function load(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      jsx: ts.JsxEmit.ReactJSX,
      target: ts.ScriptTarget.ES2022,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)((name) => {
    if (name === 'react/jsx-runtime') return jsx;
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return dependencies[name];
  }, exports);
  return exports;
}
const { modules } = load('shared/config/modules.ts');
const labels = load('features/identity/utils/permissionLabels.ts', {
  '@/shared/config/modules': { modules },
});
const { SYSTEM_ROLES } = load('features/identity/constants/systemRoles.ts');
function walk(node, predicate) {
  if (!node || typeof node !== 'object') return [];
  if (Array.isArray(node)) return node.flatMap((item) => walk(item, predicate));
  return [...(predicate(node) ? [node] : []), ...walk(node.props?.children, predicate)];
}
function harness() {
  const slots = [];
  let cursor = 0;
  let tree;
  let blockerDirty = false;
  const writes = [];
  const roleQueries = SYSTEM_ROLES.map((role) => ({
    isSuccess: true,
    isError: false,
    isFetching: false,
    data: { roleId: role.id, permissions: [{ id: role.id * 10 }] },
  }));
  const mutation = {
    isPending: false,
    isError: false,
    reset() {},
    mutate(value) {
      writes.push(value);
    },
  };
  const components = Object.fromEntries(
    [
      'PageHeader',
      'Button',
      'ConfirmDialog',
      'IdentityNavigation',
      'RoleListCard',
      'RolePermissionEditor',
    ].map((name) => [name, { [name]: name }]),
  );
  const dependencies = {
    react: {
      useEffect() {},
      useState(initial) {
        const index = cursor++;
        if (!(index in slots)) slots[index] = initial;
        return [
          slots[index],
          (next) => {
            slots[index] = typeof next === 'function' ? next(slots[index]) : next;
          },
        ];
      },
    },
    '@tanstack/react-query': {
      useQueryClient: () => ({ setQueryData() {} }),
      useQuery: () => ({
        isSuccess: true,
        data: [{ module: 'Identity', permissions: [{ id: 40 }, { id: 41 }] }],
      }),
      useQueries: () => roleQueries,
      useMutation: () => mutation,
    },
    'react-router-dom': {
      useBlocker: (dirty) => {
        blockerDirty = dirty;
        return { state: 'unblocked' };
      },
    },
    '@/components/ui/page-header': components.PageHeader,
    '@/components/ui/button': components.Button,
    '@/components/ui/confirm-dialog': components.ConfirmDialog,
    '../components/users/IdentityNavigation': components.IdentityNavigation,
    '../components/role-permissions/RoleListCard': components.RoleListCard,
    '../components/role-permissions/RolePermissionEditor': components.RolePermissionEditor,
    '../constants/systemRoles': { SYSTEM_ROLES },
    '../services/rolePermissionApi': { rolePermissionApi: {} },
  };
  const { RolePermissionPage } = load(
    'features/identity/pages/RolePermissionPage.tsx',
    dependencies,
  );
  const render = () => {
    cursor = 0;
    tree = RolePermissionPage();
  };
  const props = (type) => walk(tree, (node) => node.type === type)[0]?.props;
  render();
  return {
    render,
    props,
    roleQueries,
    writes,
    mutation,
    get blocked() {
      return blockerDirty;
    },
  };
}

test('module labels reuse Vietnamese product labels and preserve unknown module names', () => {
  for (const module of modules)
    assert.equal(labels.permissionModuleLabel(module.key), module.label);
  assert.equal(labels.permissionModuleLabel('General'), 'Quyền chung');
  assert.equal(labels.permissionModuleLabel('CustomModule'), 'CustomModule');
});

test('unsaved permission edits block role changes and navigation until discarded', () => {
  const page = harness();
  const lecturer = SYSTEM_ROLES.find((role) => role.id === 4);
  page.props('RoleListCard').onSelectRole(lecturer);
  page.render();
  page.props('RolePermissionEditor').onTogglePermission(41, true);
  page.render();
  assert.equal(page.blocked, true);
  page.props('RoleListCard').onSelectRole(SYSTEM_ROLES.find((role) => role.id === 5));
  page.render();
  assert.equal(page.props('RolePermissionEditor').role.id, 4);
  assert.equal(page.props('ConfirmDialog').open, true);
  page.props('ConfirmDialog').onClose();
  page.render();
  assert.ok(page.props('RolePermissionEditor').selectedPermissionIds.has(41));
  page.props('RolePermissionEditor').onSave();
  assert.deepEqual(page.writes, [{ roleId: 4, ids: [40, 41] }]);
  page.props('RoleListCard').onSelectRole(SYSTEM_ROLES.find((role) => role.id === 5));
  page.render();
  page.props('ConfirmDialog').onConfirm();
  page.render();
  assert.equal(page.props('RolePermissionEditor').role.id, 5);
  assert.deepEqual([...page.props('RolePermissionEditor').selectedPermissionIds], [50]);
  assert.equal(page.blocked, false);
});

test('failed role loads hide the editor; loading and saving prevent mutations', () => {
  const page = harness();
  page.props('RolePermissionEditor').onTogglePermission(41, true);
  page.render();
  assert.equal(page.blocked, false); // protected administrator
  const lecturer = SYSTEM_ROLES.find((role) => role.id === 4);
  page.props('RoleListCard').onSelectRole(lecturer);
  page.render();
  page.roleQueries[SYSTEM_ROLES.indexOf(lecturer)].isFetching = true;
  page.render();
  assert.equal(page.props('RolePermissionEditor').isLoading, true);
  page.props('RolePermissionEditor').onTogglePermission(41, true);
  page.props('RolePermissionEditor').onSave();
  page.render();
  assert.equal(page.writes.length, 0);
  assert.equal(page.blocked, false);
  page.roleQueries[SYSTEM_ROLES.indexOf(lecturer)].isFetching = false;
  page.mutation.isPending = true;
  page.render();
  page.props('RoleListCard').onSelectRole(SYSTEM_ROLES.find((role) => role.id === 5));
  page.render();
  assert.equal(page.props('RolePermissionEditor').role.id, 4);
  page.mutation.isPending = false;
  Object.assign(page.roleQueries[SYSTEM_ROLES.indexOf(lecturer)], {
    isSuccess: false,
    isError: true,
    error: new Error('Không tải được quyền'),
  });
  page.render();
  assert.equal(page.props('RolePermissionEditor'), undefined);
});
