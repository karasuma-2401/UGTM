import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function loadTsModule(path, dependencies = {}) {
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
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

function collectElements(node, predicate, out = []) {
  if (!React.isValidElement(node)) return out;
  if (predicate(node)) out.push(node);
  for (const child of React.Children.toArray(node.props.children)) {
    collectElements(child, predicate, out);
  }
  return out;
}

test('PeriodSummaryCard disables open button and shows warning when another batch is already open', () => {
  const Button = ({ children, ...props }) => React.createElement('button', props, children);
  const { PeriodSummaryCard } = loadTsModule(
    'features/academic/components/periods/PeriodSummaryCard.tsx',
    {
      react: { default: React, ...React },
      'react/jsx-runtime': jsxRuntime,
      'lucide-react': icons,
      '@/components/ui/button': { Button },
    },
  );

  const blockedTree = PeriodSummaryCard({
    academicYear: '2026-2027',
    semester: 2,
    batchName: 'KLTN HK2 2026-2027',
    status: 'Draft',
    canEdit: true,
    onOpenBatch: () => {},
    activeOpenBatchName: 'KLTN HK1 2026-2027',
  });

  const openBtn = collectElements(blockedTree, (el) => el.type === Button)[0];
  assert.equal(openBtn.props.disabled, true);
  const notice = collectElements(blockedTree, (el) => el.props?.role === 'status');
  assert.equal(notice.length, 1);

  const allowedTree = PeriodSummaryCard({
    academicYear: '2026-2027',
    semester: 2,
    batchName: 'KLTN HK2 2026-2027',
    status: 'Draft',
    canEdit: true,
    onOpenBatch: () => {},
    activeOpenBatchName: null,
  });
  const enabledBtn = collectElements(allowedTree, (el) => el.type === Button)[0];
  assert.equal(enabledBtn.props.disabled, false);
  assert.equal(collectElements(allowedTree, (el) => el.props?.role === 'status').length, 0);
});

test('usePeriodActions.handleOpenBatch blocks API call when another batch is already open', async () => {
  const states = [];
  let cursor = 0;
  const reactMock = {
    useState(initial) {
      const idx = cursor++;
      if (!(idx in states)) states[idx] = typeof initial === 'function' ? initial() : initial;
      return [states[idx], (v) => { states[idx] = typeof v === 'function' ? v(states[idx]) : v; }];
    },
    useCallback: (fn) => fn,
  };

  let openApiCalls = 0;
  const { usePeriodActions } = loadTsModule('features/academic/hooks/periods/usePeriodActions.ts', {
    react: reactMock,
    '../../services/periodApi': {
      periodApi: {
        openBatch: async () => {
          openApiCalls++;
          return { id: 'batch-2', status: 'Open' };
        },
      },
      getPeriodErrorMessage: (err) => err?.message ?? 'Error',
    },
    '../../utils/periodDates': {
      computeDefaultGateSchedule: () => ({ initial: '', faculty: '', final: '' }),
      toUtcDeadlineIso: (d) => d,
    },
  });

  cursor = 0;
  const actions = usePeriodActions();
  const errors = [];
  await actions.handleOpenBatch({
    selectedBatchId: 'batch-2',
    currentBatch: { id: 'batch-2', status: 'Draft' },
    otherOpenBatchName: 'KLTN HK1 2026-2027',
    onSuccess: async () => {},
    onError: (msg) => errors.push(msg),
  });

  assert.equal(openApiCalls, 0);
  assert.equal(errors.length, 1);
  assert.ok(errors[0].includes('KLTN HK1 2026-2027'));
});
