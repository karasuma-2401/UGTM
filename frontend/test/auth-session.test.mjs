import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';

// Execute the actual TypeScript modules with controlled HTTP/storage boundaries.
function loadModule(path, dependencies, storage = new Map()) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  });
  const exports = {};
  const localStorage = { setItem: (key, value) => storage.set(key, value) };
  new Function('require', 'exports', 'localStorage', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    localStorage,
  );
  return exports;
}

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}

const profile = { id: '1', fullName: 'Test User', roles: [], permissions: [] };
const success = { success: true, data: { accessToken: 'new-token', user: profile } };

function refreshFixture(post) {
  const saved = new Map();
  const module = loadModule(
    'shared/api/sessionRefresh.ts',
    {
      axios: { default: { post, isAxiosError: (error) => Boolean(error.isAxiosError) } },
      './config': { API_BASE_URL: 'http://api.test' },
      './csrf': { CSRF_HEADER_NAME: 'X-CSRF-TOKEN', getCsrfToken: async () => 'csrf' },
      './tokenStorage': { tokenStorage: { setAccessToken: (token) => saved.set('token', token) } },
    },
    saved,
  );
  return { ...module, saved };
}

test('concurrent startup/refresh callers share one rotating request and send CSRF/cookies', async () => {
  const pending = deferred();
  const requests = [];
  const fixture = refreshFixture((...args) => {
    requests.push(args);
    return pending.promise;
  });
  const first = fixture.refreshSession();
  const second = fixture.refreshSession();
  assert.equal(first, second);
  await Promise.resolve();
  assert.equal(requests.length, 1);
  assert.equal(requests[0][2].withCredentials, true);
  assert.equal(requests[0][2].headers['X-CSRF-TOKEN'], 'csrf');
  pending.resolve({ data: success });
  await first;
  assert.equal(fixture.saved.get('token'), 'new-token');
});

test('logout waits for a pending rotation and prevents a late refresh restoring tokens', async () => {
  const pending = deferred();
  const fixture = refreshFixture(() => pending.promise);
  const refresh = fixture.refreshSession();
  const rejected = assert.rejects(refresh);
  await Promise.resolve();
  let stopped = false;
  const stop = fixture.stopSessionRefresh().then(() => {
    stopped = true;
  });
  await Promise.resolve();
  assert.equal(stopped, false);
  pending.resolve({ data: success });
  await Promise.all([rejected, stop]);
  assert.equal(fixture.saved.has('token'), false);
  await assert.rejects(fixture.refreshSession());
  fixture.allowSessionRefresh();
  await fixture.refreshSession();
  assert.equal(fixture.saved.get('token'), 'new-token');
});

test('failed refresh can be retried after connectivity returns', async () => {
  let calls = 0;
  const fixture = refreshFixture(async () => {
    if (++calls === 1) throw new Error('Offline');
    return { data: success };
  });
  await assert.rejects(fixture.refreshSession());
  await fixture.refreshSession();
  assert.equal(calls, 2);
});

test('startup uses the current server profile instead of cached permissions', async () => {
  let refreshCalls = 0;
  const fixture = loadModule('shared/auth/restoreSession.ts', {
    '@/features/auth/services/authApi': {
      getCurrentUserApi: async () => ({ success: true, data: profile }),
      refreshTokenApi: async () => {
        refreshCalls++;
      },
    },
    '../api/tokenStorage': { tokenStorage: { hasTokens: () => true } },
  });
  assert.deepEqual(await fixture.restoreSession(), profile);
  assert.equal(refreshCalls, 0);
});

test('cookie-only startup restores once even when effects run twice', async () => {
  let refreshCalls = 0;
  let meCalls = 0;
  const fixture = loadModule('shared/auth/restoreSession.ts', {
    '@/features/auth/services/authApi': {
      getCurrentUserApi: async () => {
        meCalls++;
        return { success: true, data: profile };
      },
      refreshTokenApi: async () => {
        refreshCalls++;
      },
    },
    '../api/tokenStorage': { tokenStorage: { hasTokens: () => false } },
  });
  const first = fixture.restoreSession();
  assert.equal(first, fixture.restoreSession());
  await first;
  assert.equal(refreshCalls, 1);
  assert.equal(meCalls, 1);
});

function interceptorFixture(refreshError) {
  let handler;
  let cleared = 0;
  let events = 0;
  let refreshes = 0;
  let token = 'old-token';
  const requests = [];
  const client = async (config) => {
    requests.push(config);
    return { status: 200 };
  };
  client.interceptors = {
    request: { use() {} },
    response: {
      use(_success, failure) {
        handler = failure;
      },
    },
  };
  loadModule('shared/api/httpClient.ts', {
    axios: {
      default: { create: () => client, isAxiosError: (error) => Boolean(error.isAxiosError) },
    },
    './config': { API_BASE_URL: '' },
    './authEvents': {
      authEvents: {
        emitUnauthenticated() {
          events++;
        },
      },
    },
    './csrf': { clearCsrfToken() {} },
    './sessionRefresh': {
      isInvalidSession: (error) => error.response?.status === 401,
      refreshSession: async () => {
        refreshes++;
        if (refreshError) throw refreshError;
        token = 'new-token';
        return success;
      },
    },
    './tokenStorage': {
      tokenStorage: {
        getAccessToken: () => token,
        clearTokens() {
          cleared++;
        },
      },
    },
  });
  const error = (retry = false) => ({
    isAxiosError: true,
    response: { status: 401 },
    config: { _retry: retry, headers: { Authorization: 'Bearer old-token' } },
  });
  return { handler, error, requests, counts: () => ({ cleared, events, refreshes }) };
}

test('refresh outages preserve session data for retry', async () => {
  const fixture = interceptorFixture({ isAxiosError: true, response: { status: 503 } });
  await assert.rejects(fixture.handler(fixture.error()));
  assert.deepEqual(fixture.counts(), { cleared: 0, events: 0, refreshes: 1 });
});

test('a rejected refresh clears the local session and emits unauthenticated', async () => {
  const fixture = interceptorFixture({ isAxiosError: true, response: { status: 401 } });
  await assert.rejects(fixture.handler(fixture.error()));
  assert.deepEqual(fixture.counts(), { cleared: 1, events: 1, refreshes: 1 });
});

test('a retried request cannot enter an infinite refresh loop', async () => {
  const fixture = interceptorFixture();
  await assert.rejects(fixture.handler(fixture.error(true)));
  assert.deepEqual(fixture.counts(), { cleared: 1, events: 1, refreshes: 0 });
});

test('a delayed 401 uses the token already refreshed by another request', async () => {
  const fixture = interceptorFixture();
  await fixture.handler(fixture.error());
  await fixture.handler(fixture.error());
  assert.equal(fixture.counts().refreshes, 1);
  assert.equal(fixture.requests[1].headers.Authorization, 'Bearer new-token');
  assert.equal(fixture.requests[1]._retry, true);
});

test('activation sends the token/password/identifier to the actual backend routes', async () => {
  const requests = [];
  const fixture = loadModule('features/auth/services/authApi.ts', {
    axios: {
      default: {
        create: (options) => {
          assert.equal(options.withCredentials, true);
          return {
            post: async (...args) => {
              requests.push(args);
              return { data: { success: true, data: null } };
            },
          };
        },
      },
    },
    '@/shared/api/config': { API_BASE_URL: '' },
    '@/shared/api/csrf': {
      clearCsrfToken() {},
      CSRF_HEADER_NAME: 'X-CSRF-TOKEN',
      getCsrfToken: async () => 'csrf',
    },
    '@/shared/api/httpClient': { httpClient: {} },
    '@/shared/api/sessionRefresh': { refreshSession() {} },
  });
  await fixture.validateActivationApi('email-token');
  await fixture.completeActivationApi('email-token', 'StrongPassword!2026');
  await fixture.resendActivationApi('student@example.test');
  assert.deepEqual(requests, [
    ['/api/identity/auth/activation/validate', { token: 'email-token' }],
    [
      '/api/identity/auth/activation/complete',
      { token: 'email-token', password: 'StrongPassword!2026' },
    ],
    ['/api/identity/auth/activation/resend', { identifier: 'student@example.test' }],
  ]);
});

test('logout sends CSRF to the backend and clears cached CSRF even if it fails', async () => {
  let cleared = 0;
  const fixture = loadModule('features/auth/services/authApi.ts', {
    axios: {
      default: {
        create: () => ({
          post: async (path, _body, config) => {
            assert.equal(path, '/api/identity/auth/logout');
            assert.equal(config.headers['X-CSRF-TOKEN'], 'csrf');
            throw new Error('Offline');
          },
        }),
      },
    },
    '@/shared/api/config': { API_BASE_URL: '' },
    '@/shared/api/csrf': {
      clearCsrfToken() {
        cleared++;
      },
      CSRF_HEADER_NAME: 'X-CSRF-TOKEN',
      getCsrfToken: async () => 'csrf',
    },
    '@/shared/api/httpClient': { httpClient: {} },
    '@/shared/api/sessionRefresh': { refreshSession() {} },
  });
  await assert.rejects(fixture.logoutApi());
  assert.equal(cleared, 1);
});
