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
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    matchMedia: () => ({ matches: false, addEventListener() {}, removeEventListener() {} }),
    addEventListener() {},
    removeEventListener() {},
    ...browser.window,
  };
  const defaultDocument = {
    documentElement: {
      classList: { toggle() {} },
      dataset: {},
      style: {},
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
  return exports;
}

test('ThemeProvider resolves light, dark, and system preferences from localStorage and matchMedia', () => {
  function renderWithStorage(storedValue, systemDark) {
    const { ThemeProvider, useTheme } = loadThemeModule(
      'shared/theme/ThemeContext.tsx',
      {
        react: React,
        'react/jsx-runtime': jsxRuntime,
      },
      {
        window: {
          localStorage: {
            getItem: (key) => (key === 'ugtm:theme' ? storedValue : null),
            setItem() {},
            removeItem() {},
          },
          matchMedia: () => ({
            matches: systemDark,
            addEventListener() {},
            removeEventListener() {},
          }),
        },
      },
    );

    function Probe() {
      const { preference, resolvedTheme } = useTheme();
      return React.createElement('span', {
        'data-pref': preference,
        'data-resolved': resolvedTheme,
      });
    }

    return renderToStaticMarkup(React.createElement(ThemeProvider, null, React.createElement(Probe)));
  }

  assert.match(renderWithStorage(null, false), /data-pref="system" data-resolved="light"/);
  assert.match(renderWithStorage('invalid-theme', true), /data-pref="system" data-resolved="dark"/);
  assert.match(renderWithStorage('dark', false), /data-pref="dark" data-resolved="dark"/);
  assert.match(renderWithStorage('light', true), /data-pref="light" data-resolved="light"/);
});

test('ThemeToggle renders accessible labels and variant styles cleanly', () => {
  function renderToggle(preference, resolvedTheme, variant) {
    const { ThemeToggle } = loadThemeModule('shared/theme/ThemeToggle.tsx', {
      react: React,
      'react/jsx-runtime': jsxRuntime,
      'lucide-react': icons,
      '@/lib/utils': { cn: (...parts) => parts.filter(Boolean).join(' ') },
      './ThemeContext': {
        useTheme: () => ({
          preference,
          resolvedTheme,
          setPreference() {},
          cycleTheme() {},
        }),
      },
    });
    return renderToStaticMarkup(React.createElement(ThemeToggle, { variant }));
  }

  const headerHtml = renderToggle('dark', 'dark', 'header');
  assert.match(headerHtml, /aria-haspopup="menu"/);
  assert.match(headerHtml, /aria-expanded="false"/);
  assert.match(headerHtml, /Giao diện: Tối/);
  assert.match(headerHtml, /border-line-input\/90 bg-surface-muted\/70/);

  const authHtml = renderToggle('system', 'light', 'auth');
  assert.match(authHtml, /Giao diện: Theo hệ thống/);
  assert.match(authHtml, /backdrop-blur-sm/);
});

test('semantic CSS tokens in index.css define both :root light and .dark Cool Slate palettes without breaking print surface', () => {
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
});

test('semantic Tailwind color utilities merge cleanly without conflicting classes', () => {
  assert.equal(twMerge('bg-surface-card', 'bg-surface-subtle/70'), 'bg-surface-subtle/70');
  assert.equal(twMerge('text-ink-primary', 'text-ink-muted'), 'text-ink-muted');
  assert.equal(twMerge('border border-line/80', 'border-line-input'), 'border border-line-input');
  assert.equal(twMerge('bg-brand-solid', 'bg-tone-danger-strong'), 'bg-tone-danger-strong');
});
