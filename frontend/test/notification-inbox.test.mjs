import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
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
  new Function('require', 'exports', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name} in ${path}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

test('notificationApi calls all 6 In-App and FCM device endpoints with expected payloads', async () => {
  const calls = [];
  const httpClient = {
    async get(url, config) {
      calls.push({ method: 'GET', url, params: config?.params });
      if (url === '/api/notifications/unread-count') {
        return { data: { success: true, data: { unreadCount: 5 }, error: null } };
      }
      return {
        data: {
          success: true,
          data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 20, totalPages: 0, hasPreviousPage: false, hasNextPage: false },
          error: null,
        },
      };
    },
    async patch(url) {
      calls.push({ method: 'PATCH', url });
      return { data: { success: true, data: { updatedCount: 3 }, error: null } };
    },
    async post(url, body) {
      calls.push({ method: 'POST', url, body });
      return {
        data: {
          success: true,
          data: { id: 'dev-1', installationId: body.installationId, platform: 'web', lastSeenAt: '2026-11-25T00:00:00Z', isActive: true },
          error: null,
        },
      };
    },
    async delete(url) {
      calls.push({ method: 'DELETE', url });
      return { data: { success: true, data: null, error: null } };
    },
  };

  const { notificationApi } = loadModule('shared/notifications/notificationApi.ts', {
    '@/shared/api': { httpClient },
  });

  await notificationApi.getNotifications({ pageNumber: 2, pageSize: 10, unreadOnly: true });
  assert.equal(await notificationApi.getUnreadCount(), 5);
  await notificationApi.markAsRead('notif-1');
  assert.equal(await notificationApi.markAllAsRead(), 3);
  const dev = await notificationApi.registerDevice({
    installationId: 'inst-abc',
    token: 'fcm-token-xyz',
    platform: 'web',
  });
  assert.equal(dev.installationId, 'inst-abc');
  await notificationApi.revokeDevice('inst-abc');

  assert.deepEqual(
    calls.map((c) => `${c.method} ${c.url}`),
    [
      'GET /api/notifications',
      'GET /api/notifications/unread-count',
      'PATCH /api/notifications/notif-1/read',
      'PATCH /api/notifications/read-all',
      'POST /api/notifications/devices',
      'DELETE /api/notifications/devices/inst-abc',
    ],
  );
  assert.deepEqual(calls[0].params, { pageNumber: 2, pageSize: 10, unreadOnly: true });
});

test('useNotificationInbox scopes queries by user.id and only fetches list when popover is open', () => {
  const queries = [];
  const { useNotificationInbox } = loadModule('shared/notifications/useNotificationInbox.ts', {
    '@tanstack/react-query': {
      useQueryClient: () => ({ invalidateQueries: () => {} }),
      useQuery(opts) {
        queries.push(opts);
        return { data: undefined, isLoading: false, isError: false, refetch: () => {} };
      },
      useMutation: () => ({ mutate: () => {}, isPending: false }),
    },
    '@/shared/auth': { useAuth: () => ({ user: { id: 'u-99' } }) },
    './notificationApi': { notificationApi: {} },
  });

  useNotificationInbox({ open: false, filter: 'unread' });
  assert.deepEqual(queries[0].queryKey, ['notifications-unread-count', 'u-99']);
  assert.equal(queries[0].enabled, true);
  assert.deepEqual(queries[1].queryKey, ['notifications-inbox', 'u-99', 'unread']);
  assert.equal(queries[1].enabled, false);
});

test('NotificationBell renders unread badge (capped at 99+) and formats relative Vietnamese timestamps', () => {
  function renderBell(unreadCount) {
    const mod = loadModule('shared/notifications/NotificationBell.tsx', {
      react: React,
      'react/jsx-runtime': jsxRuntime,
      'react-router-dom': { useNavigate: () => () => {} },
      'lucide-react': icons,
      '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
      '@/components/ui/button': {
        Button: ({ children, ...rest }) => React.createElement('button', rest, children),
      },
      '@/components/ui/skeleton': {
        Skeleton: ({ className }) => React.createElement('span', { className }),
      },
      './useNotificationInbox': {
        useNotificationInbox: () => ({
          unreadCount,
          listQuery: { isLoading: false, isError: false, refetch: () => {} },
          items: [],
          markReadMutation: { mutate: () => {}, isPending: false },
          markAllReadMutation: { mutate: () => {}, isPending: false },
        }),
      },
    });
    return {
      html: renderToStaticMarkup(React.createElement(mod.NotificationBell)),
      format: mod.formatNotificationRelativeTime,
    };
  }

  const zero = renderBell(0);
  assert.match(zero.html, /aria-label="Mở thông báo"/);
  assert.doesNotMatch(zero.html, /99\+/);

  const capped = renderBell(142);
  assert.match(capped.html, /aria-label="Mở thông báo \(142 chưa đọc\)"/);
  assert.match(capped.html, />99\+</);

  const base = Date.parse('2026-11-25T10:00:00Z');
  assert.equal(zero.format('2026-11-25T09:59:30Z', base), 'Vừa xong');
  assert.equal(zero.format('2026-11-25T09:45:00Z', base), '15 phút trước');
  assert.equal(zero.format('2026-11-25T07:00:00Z', base), '3 giờ trước');
  assert.equal(zero.format('2026-11-23T10:00:00Z', base), '2 ngày trước');
});
