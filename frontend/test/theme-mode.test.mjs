import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';
import { renderToStaticMarkup } from 'react-dom/server';
import { twMerge } from 'tailwind-merge';

function loadThemeModule(path, dependencies = {}, browser = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  const defaultWindow = {
    innerWidth: 1280,
    innerHeight: 800,
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    matchMedia: () => ({ matches: false, addEventListener() {}, removeEventListener() {} }),
    addEventListener() {},
    removeEventListener() {},
    ...browser.window,
  };
  const classSet = new Set();
  const defaultDocument = {
    documentElement: {
      classList: {
        toggle(cls, force) {
          if (force === undefined) {
            if (classSet.has(cls)) classSet.delete(cls);
            else classSet.add(cls);
          } else if (force) classSet.add(cls);
          else classSet.delete(cls);
        },
        add(cls) {
          classSet.add(cls);
        },
        remove(cls) {
          classSet.delete(cls);
        },
        contains(cls) {
          return classSet.has(cls);
        },
      },
      dataset: {},
      style: {},
      animate() {
        return { finished: Promise.resolve() };
      },
    },
    addEventListener() {},
    removeEventListener() {},
    ...browser.document,
  };

  new Function('require', 'exports', 'window', 'localStorage', 'document', 'HTMLElement', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
    defaultWindow,
    defaultWindow.localStorage,
    defaultDocument,
    class {},
  );
  return { exports, document: defaultDocument, window: defaultWindow };
}

test('ThemeProvider defaults to light (ignoring legacy system or invalid storage) and reads light/dark', () => {
  function renderWithStorage(storedValue, osDark) {
    const { exports } = loadThemeModule(
      'shared/theme/ThemeContext.tsx',
      {
        react: React,
        'react/jsx-runtime': jsxRuntime,
        'react-dom': { flushSync: (fn) => fn() },
      },
      {
        window: {
          localStorage: {
            getItem: (key) => (key === 'ugtm:theme' ? storedValue : null),
            setItem() {},
            removeItem() {},
          },
          matchMedia: () => ({
            matches: osDark,
            addEventListener() {},
            removeEventListener() {},
          }),
        },
      },
    );
    const { ThemeProvider, useTheme } = exports;

    function Probe() {
      const { theme, resolvedTheme } = useTheme();
      return React.createElement('span', {
        'data-theme': theme,
        'data-resolved': resolvedTheme,
      });
    }

    return renderToStaticMarkup(React.createElement(ThemeProvider, null, React.createElement(Probe)));
  }

  assert.match(renderWithStorage(null, true), /data-theme="light" data-resolved="light"/);
  assert.match(renderWithStorage('system', true), /data-theme="light" data-resolved="light"/);
  assert.match(renderWithStorage('invalid-theme', true), /data-theme="light" data-resolved="light"/);
  assert.match(renderWithStorage('dark', false), /data-theme="dark" data-resolved="dark"/);
  assert.match(renderWithStorage('light', true), /data-theme="light" data-resolved="light"/);
});

test('ThemeContext animates circular View Transition from sourceElement center and respects reduced motion', async () => {
  let stored = 'light';
  let flushCount = 0;
  const animations = [];
  let resolveReady;
  let resolveFinished;
  const readyPromise = new Promise((r) => {
    resolveReady = r;
  });
  const finishedPromise = new Promise((r) => {
    resolveFinished = r;
  });

  const { exports, document: doc } = loadThemeModule(
    'shared/theme/ThemeContext.tsx',
    {
      react: {
        ...React,
        useState: (init) => [typeof init === 'function' ? init() : init, () => {}],
        useRef: (init) => ({ current: init }),
        useEffect: () => {},
        useCallback: (fn) => fn,
        useMemo: (fn) => fn(),
      },
      'react/jsx-runtime': jsxRuntime,
      'react-dom': {
        flushSync: (fn) => {
          flushCount += 1;
          return fn();
        },
      },
    },
    {
      window: {
        innerWidth: 1000,
        innerHeight: 800,
        localStorage: {
          getItem: (key) => (key === 'ugtm:theme' ? stored : null),
          setItem: (key, val) => {
            if (key === 'ugtm:theme') stored = val;
          },
          removeItem() {},
        },
        matchMedia: () => ({ matches: false, addEventListener() {}, removeEventListener() {} }),
      },
      document: {
        startViewTransition(cb) {
          cb();
          return { ready: readyPromise, finished: finishedPromise };
        },
      },
    },
  );

  // Preserve custom startViewTransition while ensuring documentElement methods exist
  doc.documentElement.animate = (keyframes, options) => {
    animations.push({ keyframes, options });
    return { finished: Promise.resolve() };
  };

  let ctx;
  function Capture() {
    ctx = exports.useTheme();
    return null;
  }
  renderToStaticMarkup(React.createElement(exports.ThemeProvider, null, React.createElement(Capture)));

  let beforeCommitCalled = false;
  ctx.toggleTheme({
    sourceElement: {
      getBoundingClientRect: () => ({ left: 900, top: 12, width: 36, height: 36 }),
    },
    onBeforeCommit: () => {
      beforeCommitCalled = true;
    },
  });

  assert.equal(stored, 'dark');
  assert.equal(flushCount, 1);
  assert.equal(beforeCommitCalled, true);
  assert.equal(doc.documentElement.classList.contains('dark'), true);
  assert.equal(doc.documentElement.classList.contains('theme-transitioning'), true);

  resolveReady();
  await Promise.resolve();
  await Promise.resolve();
  assert.equal(animations.length, 1);
  assert.deepEqual(animations[0].keyframes.clipPath, [
    'circle(0px at 918px 30px)',
    `circle(${Math.hypot(918, 770)}px at 918px 30px)`,
  ]);
  assert.equal(animations[0].options.pseudoElement, '::view-transition-new(root)');

  resolveFinished();
  await Promise.resolve();
  await Promise.resolve();
  assert.equal(doc.documentElement.classList.contains('theme-transitioning'), false);

  // Clicking the already active theme runs onBeforeCommit without starting a redundant transition
  let redundantBeforeCommit = false;
  ctx.setTheme('dark', {
    origin: { x: 100, y: 100 },
    onBeforeCommit: () => {
      redundantBeforeCommit = true;
    },
  });
  assert.equal(redundantBeforeCommit, true);
  assert.equal(animations.length, 1);
});

test('ThemeContext skips startViewTransition when prefers-reduced-motion is reduce', () => {
  let stored = 'light';
  let viewTransitionCalled = false;
  const { exports, document: doc } = loadThemeModule(
    'shared/theme/ThemeContext.tsx',
    {
      react: {
        ...React,
        useState: (init) => [typeof init === 'function' ? init() : init, () => {}],
        useRef: (init) => ({ current: init }),
        useEffect: () => {},
        useCallback: (fn) => fn,
        useMemo: (fn) => fn(),
      },
      'react/jsx-runtime': jsxRuntime,
      'react-dom': { flushSync: (fn) => fn() },
    },
    {
      window: {
        innerWidth: 1000,
        innerHeight: 800,
        localStorage: {
          getItem: (key) => (key === 'ugtm:theme' ? stored : null),
          setItem: (key, val) => {
            if (key === 'ugtm:theme') stored = val;
          },
          removeItem() {},
        },
        matchMedia: (q) => ({
          matches: q === '(prefers-reduced-motion: reduce)',
          addEventListener() {},
          removeEventListener() {},
        }),
      },
      document: {
        startViewTransition() {
          viewTransitionCalled = true;
          return { ready: Promise.resolve(), finished: Promise.resolve() };
        },
      },
    },
  );

  let ctx;
  function Capture() {
    ctx = exports.useTheme();
    return null;
  }
  renderToStaticMarkup(React.createElement(exports.ThemeProvider, null, React.createElement(Capture)));

  ctx.toggleTheme({ origin: { x: 500, y: 30 } });
  assert.equal(viewTransitionCalled, false);
  assert.equal(stored, 'dark');
  assert.equal(doc.documentElement.classList.contains('dark'), true);
});

test('ThemeToggle renders a circular 1-click button with accessible action labels and no dropdown', () => {
  function renderToggle(theme) {
    const { exports } = loadThemeModule('shared/theme/ThemeToggle.tsx', {
      react: React,
      'react/jsx-runtime': jsxRuntime,
      'lucide-react': icons,
      '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
      './ThemeContext': {
        useTheme: () => ({
          theme,
          resolvedTheme: theme,
          setTheme() {},
          toggleTheme() {},
        }),
      },
    });
    return renderToStaticMarkup(React.createElement(exports.ThemeToggle));
  }

  const lightHtml = renderToggle('light');
  assert.match(lightHtml, /rounded-full/);
  assert.match(lightHtml, /aria-label="Chuyển sang giao diện tối"/);
  assert.doesNotMatch(lightHtml, /aria-haspopup="menu"/);
  assert.doesNotMatch(lightHtml, /Theo hệ thống/);

  const darkHtml = renderToggle('dark');
  assert.match(darkHtml, /rounded-full/);
  assert.match(darkHtml, /aria-label="Chuyển sang giao diện sáng"/);
});

test('AuthLayout omits ThemeToggle and AppLayout Ctrl+K only offers Sáng and Tối', () => {
  const authLayoutSource = readFileSync(
    new URL('../src/features/auth/layouts/AuthLayout.tsx', import.meta.url),
    'utf8',
  );
  assert.doesNotMatch(authLayoutSource, /ThemeToggle/);

  const appLayoutSource = readFileSync(
    new URL('../src/shared/layout/AppLayout.tsx', import.meta.url),
    'utf8',
  );
  assert.doesNotMatch(appLayoutSource, /label:\s*['"]Hệ thống['"]/);
  assert.match(appLayoutSource, /label:\s*['"]Sáng['"]/);
  assert.match(appLayoutSource, /label:\s*['"]Tối['"]/);
});

test('semantic CSS tokens in index.css define both :root light and .dark Cool Slate palettes and View Transition rules', () => {
  const indexCss = readFileSync(new URL('../src/index.css', import.meta.url), 'utf8');
  for (const token of [
    '--surface-canvas:',
    '--surface-card:',
    '--surface-elevated:',
    '--surface-subtle:',
    '--ink-primary:',
    '--ink-heading:',
    '--ink-muted:',
    '--line-subtle:',
    '--line-input:',
    '--brand-ink:',
    '--brand-solid:',
    '--tone-info-bg:',
    '--tone-danger-text:',
  ]) {
    const matches = indexCss.match(new RegExp(token, 'g')) ?? [];
    assert.ok(
      matches.length >= 2,
      `Expected ${token} to be declared in both :root and .dark in src/index.css`,
    );
  }
  assert.match(indexCss, /--surface-canvas:\s*9 13 22;/);
  assert.match(indexCss, /--surface-card:\s*15 23 42;/);
  assert.match(indexCss, /::view-transition-new\(root\)/);
  assert.match(indexCss, /html\.theme-transitioning/);
});

test('semantic Tailwind color utilities merge cleanly without conflicting classes', () => {
  assert.equal(twMerge('bg-surface-card', 'bg-surface-subtle/70'), 'bg-surface-subtle/70');
  assert.equal(twMerge('text-ink-primary', 'text-ink-muted'), 'text-ink-muted');
  assert.equal(twMerge('border border-line/80', 'border-line-input'), 'border border-line-input');
  assert.equal(twMerge('bg-brand-solid', 'bg-tone-danger-strong'), 'bg-tone-danger-strong');
});
