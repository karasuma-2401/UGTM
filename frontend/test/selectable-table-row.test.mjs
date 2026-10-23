import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import { renderToStaticMarkup } from 'react-dom/server';
import { clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

const source = readFileSync(
  new URL('../src/components/ui/selectable-table-row.tsx', import.meta.url),
  'utf8',
);
const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022,
    jsx: ts.JsxEmit.ReactJSX,
  },
});
const exports = {};
new Function('require', 'exports', outputText)((name) => {
  const dependencies = {
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn: (...values) => twMerge(clsx(values)) },
  };
  assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
  return dependencies[name];
}, exports);
const { SelectableTableRow } = exports;

test('selection preserves table markup and adds an accessible state and left marker', () => {
  const markup = (selected) => renderToStaticMarkup(
    React.createElement('table', null,
      React.createElement('tbody', null,
        React.createElement(SelectableTableRow, { selected, onSelect() {} },
          React.createElement('td', null, 'Account'),
        ),
      ),
    ),
  );
  const selected = markup(true);
  assert.match(selected, /<tbody><tr/);
  assert.match(selected, /aria-selected="true"/);
  assert.match(selected, /before:left-0/);
  assert.match(selected, /before:rounded-r/);
  assert.doesNotMatch(markup(false), /before:left-0/);
});

test('row click selects without invoking an action on its child button', () => {
  let selected = 0;
  let edited = 0;
  const row = SelectableTableRow({
    onSelect: () => selected++,
    children: React.createElement('td', null,
      React.createElement('button', { onClick: () => edited++ }, 'Edit'),
    ),
  });
  row.props.onClick({});
  assert.equal(selected, 1);
  assert.equal(edited, 0);
});

test('Enter and Space select the focused row without intercepting nested controls', () => {
  let selected = 0;
  let prevented = 0;
  const row = SelectableTableRow({ onSelect: () => selected++ });
  const currentTarget = {};
  const key = (value, target = currentTarget) => row.props.onKeyDown({
    key: value, currentTarget, target, preventDefault: () => prevented++,
  });
  key('Enter');
  key(' ');
  key('Escape');
  key(' ', {});
  key('Enter', {});
  assert.equal(selected, 2);
  assert.equal(prevented, 2);
});

test('loading rows cannot be selected by mouse or keyboard', () => {
  let selected = 0;
  const row = SelectableTableRow({ disabled: true, onSelect: () => selected++ });
  assert.equal(row.props.tabIndex, undefined);
  assert.equal(row.props['aria-disabled'], true);
  row.props.onClick({});
  const currentTarget = {};
  row.props.onKeyDown({ key: 'Enter', target: currentTarget, currentTarget });
  assert.equal(selected, 0);
});
