import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';

function fixture({ saved = '{}', storageBlocked = false, ...options } = {}) {
  const state = [];
  const effects = [];
  const writes = [];
  let cursor = 0;
  const source = readFileSync(
    new URL('../src/shared/layout/SidebarNavigation.tsx', import.meta.url),
    'utf8',
  );
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const dependencies = {
    react: {
      useState(initial) {
        const index = cursor++;
        if (!(index in state)) state[index] = typeof initial === 'function' ? initial() : initial;
        return [
          state[index],
          (value) => {
            state[index] = typeof value === 'function' ? value(state[index]) : value;
          },
        ];
      },
      useEffect(effect, deps) {
        const index = cursor++;
        if (!state[index] || deps.some((value, i) => value !== state[index][i]))
          effects.push(effect);
        state[index] = deps;
      },
    },
    'react/jsx-runtime': jsxRuntime,
    'react-router-dom': {
      NavLink: ({ to, end: _end, children, ...props }) =>
        React.createElement('a', { ...props, href: to }, children),
    },
    'lucide-react': icons,
    '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
    '@/components/ui/badge': {
      Badge: ({ children }) => React.createElement('span', null, children),
    },
  };
  const storage = {
    getItem(key) {
      assert.equal(key, 'ugtm:sidebar_groups:test-user');
      if (storageBlocked) throw new Error('Blocked');
      return saved;
    },
    setItem(key, value) {
      if (storageBlocked) throw new Error('Blocked');
      writes.push([key, JSON.parse(value)]);
    },
  };
  const exports = {};
  new Function('require', 'exports', 'localStorage', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    storage,
  );
  const props = {
    sections: [
      {
        id: 'planning',
        title: 'Tổ chức & kế hoạch',
        items: [{ path: '/academic/periods', label: 'Kế hoạch đợt', icon: icons.CalendarRange }],
      },
      {
        id: 'topics',
        title: 'Đề tài & hướng dẫn',
        items: [
          { path: '/topics/rename', label: 'Xin đổi tên đề tài', icon: icons.FilePenLine },
          { path: '/topics/rename/review', label: 'Duyệt đổi tên đề tài', icon: icons.FilePenLine },
        ],
      },
    ],
    pathname: '/academic/periods',
    activePath: '/academic/periods',
    collapsed: false,
    collapsible: true,
    userId: 'test-user',
    onNavigate() {},
    ...options,
  };
  function render() {
    cursor = 0;
    return exports.SidebarNavigation(props);
  }
  return {
    props,
    writes,
    render,
    flush() {
      effects.splice(0).forEach((effect) => effect());
    },
    html() {
      return renderToStaticMarkup(render());
    },
  };
}

function nodes(node) {
  if (!React.isValidElement(node)) return [];
  return [node, ...React.Children.toArray(node.props.children).flatMap(nodes)];
}

test('Admin can close a group, persist the preference, and open the target group on navigation', () => {
  const view = fixture();
  let tree = view.render();
  const toggle = nodes(tree).find(
    (node) => node.type === 'button' && node.props['aria-controls'] === 'sidebar-group-planning',
  );
  assert.equal(toggle.props['aria-expanded'], true);
  toggle.props.onClick();
  tree = view.render();
  assert.equal(
    nodes(tree).find((node) => node.props['aria-controls'] === 'sidebar-group-planning').props[
      'aria-expanded'
    ],
    false,
  );
  view.flush();
  assert.equal(view.writes.at(-1)[1].planning, false);
  view.props.pathname = '/topics/rename/review';
  view.props.activePath = '/topics/rename/review';
  view.render();
  view.flush();
  const html = view.html();
  assert.match(html, /aria-expanded="true" aria-controls="sidebar-group-topics"/);
  assert.equal((html.match(/aria-current="page"/g) ?? []).length, 1);
  assert.match(html, /aria-current="page"[^>]*href="\/topics\/rename\/review"/);
});

test('stored groups are restored while a deep-linked active group always opens', () => {
  const view = fixture({
    saved: '{"planning":false,"topics":true}',
    pathname: '/academic/periods/detail',
    activePath: '/academic/periods',
  });
  const html = view.html();
  assert.match(html, /aria-expanded="true" aria-controls="sidebar-group-planning"/);
  assert.match(html, /aria-expanded="true" aria-controls="sidebar-group-topics"/);
});

test('invalid or unavailable localStorage never prevents navigation', () => {
  for (const options of [
    { saved: '{invalid' },
    { saved: 'null' },
    { saved: '[]' },
    { storageBlocked: true },
  ]) {
    const view = fixture(options);
    assert.match(view.html(), /href="\/academic\/periods"/);
    assert.doesNotThrow(() => view.flush());
  }
});

test('non-Admin navigation stays expanded and icon rail links keep accessible names', () => {
  const view = fixture({ collapsible: false, collapsed: true });
  const html = view.html();
  assert.doesNotMatch(html, /aria-expanded=/);
  assert.match(html, /aria-label="Duyệt đổi tên đề tài"/);
  assert.match(html, /href="\/topics\/rename\/review"/);
  view.flush();
  assert.deepEqual(view.writes, []);
});
