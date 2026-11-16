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

function renderStudentLayout(pathname) {
  const { navigationSections } = loadModule('shared/layout/navigationConfig.ts', {
    'lucide-react': icons,
  });
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
        user: { fullName: 'Sinh viên thử nghiệm', roles: ['Student'] },
        hasRole: (required) =>
          (Array.isArray(required) ? required : [required]).some(
            (role) => role.toLowerCase() === 'student',
          ),
        hasPermission: () => false,
        logout: async () => {},
        isLoggingOut: false,
      }),
    },
    '@/shared/auth/identityAccess': { getIdentityAccess: () => ({ canAccess: false }) },
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
    const { markup } = renderStudentLayout(route);
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
