import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
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
  const noopWindow = {
    addEventListener() {},
    removeEventListener() {},
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
  };
  new Function('require', 'exports', 'window', 'localStorage', 'document', 'HTMLElement', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    noopWindow,
    noopWindow.localStorage,
    {},
    class {},
  );
  return exports;
}

function renderLayout(
  pathname,
  {
    roles = ['Student'],
    permissions = [],
  } = {},
) {
  const { navigationSections } = loadModule('shared/layout/navigationConfig.ts', {
    'lucide-react': icons,
  });
  const { getIdentityAccess } = loadModule('shared/auth/identityAccess.ts', {});
  const Link = ({ to, children, ...props }) =>
    React.createElement('a', { ...props, href: to }, children);
  const { SidebarNavigation } = loadModule('shared/layout/SidebarNavigation.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': { NavLink: ({ end: _end, ...props }) => React.createElement(Link, props) },
    'lucide-react': icons,
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    '@/components/ui/badge': {
      Badge: ({ children }) => React.createElement('span', null, children),
    },
  });
  const hasPermission = (required) =>
    (Array.isArray(required) ? required : [required]).some((perm) => permissions.includes(perm));
  const { AppLayout } = loadModule('shared/layout/AppLayout.tsx', {
    react: React,
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': {
      Link,
      NavLink: Link,
      Outlet: () => null,
      useLocation: () => ({ pathname }),
    },
    '../../../assets/logo_UGTM.svg': { default: '/logo.svg' },
    'lucide-react': icons,
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    '@/components/ui/badge': {
      Badge: ({ children }) => React.createElement('span', null, children),
    },
    '@/components/ui/button': {
      Button: ({ children }) => React.createElement('button', null, children),
    },
    '@/components/ui/input': { Input: () => React.createElement('input') },
    '@/shared/auth': {
      useAuth: () => ({
        user: { fullName: 'Người dùng thử nghiệm', roles },
        hasRole: (required) =>
          (Array.isArray(required) ? required : [required]).some((role) =>
            roles.some((actual) => actual.toLowerCase() === role.toLowerCase()),
          ),
        hasPermission,
        logout: async () => {},
        isLoggingOut: false,
      }),
    },
    '@/shared/auth/identityAccess': { getIdentityAccess },
    '@/shared/theme': {
      ThemeToggle: () => null,
      useTheme: () => ({ theme: 'system', resolvedTheme: 'light', setTheme: () => {}, toggleTheme: () => {} }),
    },
    './navigationConfig': { navigationSections },
    './CurrentAcademicBatch': { CurrentAcademicBatch: () => null },
    './SidebarNavigation': { SidebarNavigation },
  });

  return {
    navigationSections,
    markup: renderToStaticMarkup(React.createElement(AppLayout)),
  };
}

test('Báo cáo và tiến độ defaults to /thesis/milestones and highlights sidebar on all 4 student sub-tabs', () => {
  for (const route of [
    '/thesis/milestones',
    '/thesis/proposal',
    '/thesis/midterm',
    '/thesis/final-report',
  ]) {
    const { markup } = renderLayout(route, { roles: ['Student'] });
    const sidebarMarkup = markup.match(/<aside[\s\S]*?<\/aside>/)?.[0] ?? '';
    const progressAnchor =
      sidebarMarkup.match(/<a[^>]*aria-label="Báo cáo và tiến độ"[^>]*>/)?.[0] ?? '';

    assert.match(progressAnchor, /href="\/thesis\/milestones"/);
    assert.match(progressAnchor, /aria-current="page"/);
    assert.match(progressAnchor, /bg-primary\/10 font-semibold text-primary/);

    const archivalAnchor =
      sidebarMarkup.match(/<a[^>]*aria-label="Hồ sơ lưu chiểu"[^>]*>/)?.[0] ?? '';
    assert.doesNotMatch(archivalAnchor, /aria-current="page"/);
  }
});

test('AcademicAffairs with identity.users.read sees Tài khoản (without Phân quyền or audit logs)', () => {
  const { markup } = renderLayout('/modules/identity/students', {
    roles: ['AcademicAffairs'],
    permissions: ['identity.users.read', 'identity.users.import'],
  });
  const sidebarMarkup = markup.match(/<aside[\s\S]*?<\/aside>/)?.[0] ?? '';

  const identityAnchor =
    sidebarMarkup.match(/<a[^>]*aria-label="Tài khoản"[^>]*>/)?.[0] ?? '';
  assert.match(identityAnchor, /href="\/modules\/identity\/students"/);
  assert.match(identityAnchor, /aria-current="page"/);
  assert.doesNotMatch(sidebarMarkup, /href="\/modules\/identity\/permissions"/);
  assert.doesNotMatch(sidebarMarkup, /href="\/admin\/audit-logs"/);

  const adminLayout = renderLayout('/modules/identity/permissions', {
    roles: ['Admin'],
    permissions: ['identity.users.read', 'identity.roles.manage', 'identity.permissions.read'],
  });
  const adminSidebar = adminLayout.markup.match(/<aside[\s\S]*?<\/aside>/)?.[0] ?? '';
  assert.match(adminSidebar, /<a[^>]*aria-label="Tài khoản"[^>]*href="\/modules\/identity\/students"/);
  const permissionAnchor =
    adminSidebar.match(/<a[^>]*aria-label="Phân quyền"[^>]*>/)?.[0] ?? '';
  assert.match(permissionAnchor, /href="\/modules\/identity\/permissions"/);
  assert.match(permissionAnchor, /aria-current="page"/);

  const staffLayout = renderLayout('/modules/identity/staff', {
    roles: ['Admin'],
    permissions: ['identity.users.read', 'identity.roles.manage', 'identity.permissions.read'],
  });
  const staffSidebar = staffLayout.markup.match(/<aside[\s\S]*?<\/aside>/)?.[0] ?? '';
  const staffAccountAnchor =
    staffSidebar.match(/<a[^>]*aria-label="Tài khoản"[^>]*>/)?.[0] ?? '';
  assert.match(staffAccountAnchor, /aria-current="page"/);

  const { getIdentityAccess } = loadModule('shared/auth/identityAccess.ts', {});
  const NavLink = ({ to, children, className }) =>
    React.createElement(
      'a',
      { href: to, className: typeof className === 'function' ? className({ isActive: false }) : className },
      children,
    );
  const { IdentityNavigation } = loadModule(
    'features/identity/components/users/IdentityNavigation.tsx',
    {
      'react/jsx-runtime': jsxRuntime,
      'react-router-dom': {
        NavLink,
        useLocation: () => ({ pathname: '/modules/identity/students' }),
        useNavigate: () => () => {},
      },
      '@/components/ui/selection-indicator': { SelectionIndicator: () => null },
      'lucide-react': icons,
      '@/shared/auth': {
        useAuth: () => ({
          hasPermission: (perm) => ['identity.users.read', 'identity.users.import'].includes(perm),
        }),
      },
      '@/shared/auth/identityAccess': { getIdentityAccess },
      '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    },
  );

  const navHtml = renderToStaticMarkup(React.createElement(IdentityNavigation));
  assert.match(navHtml, /href="\/modules\/identity\/students"/);
  assert.match(navHtml, /href="\/modules\/identity\/staff"/);
  assert.doesNotMatch(navHtml, /href="\/modules\/identity\/permissions"/);
});

test('sidebar places Tiến độ sinh viên before Chấm điểm khóa luận and Thẩm định đề tài before Duyệt đổi tên đề tài', () => {
  const lecturerLayout = renderLayout('/advisor/milestones', { roles: ['Lecturer'] });
  const milestoneIdx = lecturerLayout.markup.indexOf('Tiến độ sinh viên');
  const gradingIdx = lecturerLayout.markup.indexOf('Chấm điểm khóa luận');
  assert.ok(milestoneIdx > -1 && gradingIdx > -1);
  assert.ok(milestoneIdx < gradingIdx);

  const facultyLayout = renderLayout('/topics/review', { roles: ['Faculty'] });
  const reviewIdx = facultyLayout.markup.indexOf('Thẩm định đề tài');
  const renameIdx = facultyLayout.markup.indexOf('Duyệt đổi tên đề tài');
  assert.ok(reviewIdx > -1 && renameIdx > -1);
  assert.ok(reviewIdx < renameIdx);
});

test('every sidebar navigation item uses a distinct icon', () => {
  const { navigationSections } = loadModule('shared/layout/navigationConfig.ts', {
    'lucide-react': icons,
  });
  const items = navigationSections.flatMap((section) => section.items);
  const seen = new Map();
  for (const item of items) {
    assert.ok(!seen.has(item.icon), `Duplicate icon shared between "${seen.get(item.icon)}" and "${item.label}"`);
    seen.set(item.icon, item.label);
  }
});
