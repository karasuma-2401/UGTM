import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import axios from 'axios';

// Exercise the actual route and login handlers with controlled auth/router
// boundaries, without making authentication requests or using real credentials.
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
  new Function('require', 'exports', 'localStorage', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    { getItem: () => null, setItem() {}, removeItem() {} },
  );
  return exports;
}

const Navigate = () => null;
const location = { pathname: '/modules/identity/students', search: '', hash: '' };
const defaultAuth = {
  isAuthenticated: false,
  isLoading: false,
  isLoggingOut: false,
  sessionError: '',
  sessionNotice: '',
  retrySession() {},
  hasRole: () => true,
  hasPermission: () => true,
};

function guard(auth) {
  const { ProtectedRoute } = loadModule('shared/auth/ProtectedRoute.tsx', {
    react: { default: React },
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': { Navigate, Outlet: () => null, useLocation: () => location },
    './useAuth': { useAuth: () => ({ ...defaultAuth, ...auth }) },
  });
  return ProtectedRoute({});
}

function findElement(node, predicate) {
  if (!React.isValidElement(node)) return undefined;
  if (predicate(node)) return node;
  for (const child of React.Children.toArray(node.props.children)) {
    const match = findElement(child, predicate);
    if (match) return match;
  }
}

function loginFixture(auth = {}, state = null) {
  const effects = [];
  const navigations = [];
  let logins = 0;
  const { LoginPage } = loadModule('features/auth/pages/LoginPage.tsx', {
    axios: { default: axios },
    'lucide-react': icons,
    react: {
      useState: (initial) => [typeof initial === 'function' ? initial() : initial, () => {}],
      useEffect: (effect) => effects.push(effect),
    },
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': {
      Link: () => null,
      useLocation: () => ({ pathname: '/login', state }),
      useNavigate:
        () =>
        (...args) =>
          navigations.push(args),
    },
    '../../../../assets/logo_sso.png': { default: '/sso.png' },
    '@/components/ui/button': { Button: () => null },
    '@/components/ui/input': { Input: () => null },
    '@/shared/auth': {
      useAuth: () => ({
        ...defaultAuth,
        ...auth,
        login: () => {
          logins++;
        },
      }),
    },
    '../components/PasswordField': { PasswordField: () => null },
    '../constants/auth.constants': loadModule('features/auth/constants/auth.constants.ts'),
    '../services/authApi': {
      loginApi: async () => ({
        success: true,
        data: { accessToken: 'test-token', user: { id: 'test-user' } },
      }),
    },
  });
  const tree = LoginPage();
  effects.forEach((effect) => effect());
  return { tree, navigations, logins: () => logins };
}

test('explicit logout clears the previous destination and the next login goes to home', async () => {
  const redirect = guard({ isAuthenticated: true, isLoggingOut: true });
  assert.equal(redirect.type, Navigate);
  assert.equal(redirect.props.to, '/login');
  assert.equal(redirect.props.replace, true);
  assert.equal(redirect.props.state, null);

  const login = loginFixture({}, redirect.props.state);
  const form = findElement(login.tree, (element) => element.type === 'form');
  await form.props.onSubmit({ preventDefault() {} });
  assert.equal(login.logins(), 1);
  assert.deepEqual(login.navigations, [['/', { replace: true }]]);
});

test('login waits for pending logout instead of redirecting with the old authenticated user', async () => {
  const login = loginFixture({ isAuthenticated: true, isLoggingOut: true });
  assert.deepEqual(login.navigations, []);
  const form = findElement(login.tree, (element) => element.type === 'form');
  await form.props.onSubmit({ preventDefault() {} });
  assert.equal(login.logins(), 0);
  const submit = findElement(login.tree, (element) => element.props.type === 'submit');
  assert.equal(submit.props.disabled, true);
});

test('opening a protected link without a session still returns to that page after login', async () => {
  const redirect = guard({});
  assert.equal(redirect.type, Navigate);
  assert.deepEqual(redirect.props.state, { from: location });
  const login = loginFixture({}, redirect.props.state);
  const form = findElement(login.tree, (element) => element.type === 'form');
  await form.props.onSubmit({ preventDefault() {} });
  assert.deepEqual(login.navigations, [[location.pathname, { replace: true }]]);
});

test('restoring a session does not navigate before its authentication check finishes', () => {
  const login = loginFixture({ isAuthenticated: true, isLoading: true });
  assert.deepEqual(login.navigations, []);
});

test('visiting login with a settled authenticated session opens the default page', () => {
  const login = loginFixture({ isAuthenticated: true });
  assert.deepEqual(login.navigations, [['/', { replace: true }]]);
});
