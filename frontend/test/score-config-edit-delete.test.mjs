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

function createHookRunner() {
  const states = [];
  let cursor = 0;

  const reactMock = {
    useState(initial) {
      const idx = cursor++;
      if (!(idx in states)) {
        states[idx] = typeof initial === 'function' ? initial() : initial;
      }
      const setState = (next) => {
        states[idx] = typeof next === 'function' ? next(states[idx]) : next;
      };
      return [states[idx], setState];
    },
  };

  function render(hookFn) {
    cursor = 0;
    return hookFn();
  }

  return { reactMock, render };
}

function collectElements(node, predicate, out = []) {
  if (!React.isValidElement(node)) return out;
  if (predicate(node)) out.push(node);
  for (const child of React.Children.toArray(node.props.children)) {
    collectElements(child, predicate, out);
  }
  return out;
}

const scoreConfigTypes = loadTsModule('features/academic/types/scoreConfig.types.ts');
const scoreWeights = loadTsModule('features/academic/utils/scoreWeights.ts', {
  '../types/scoreConfig.types': scoreConfigTypes,
});

function loadScoreComponentTable() {
  const passthrough =
    (tag) =>
    ({ children, ...props }) =>
      React.createElement(tag, props, children);
  return loadTsModule('features/academic/components/score-config/ScoreComponentTable.tsx', {
    react: { default: React, ...React },
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@/components/ui/button': { Button: passthrough('button') },
    '@/components/ui/badge': { Badge: passthrough('span') },
    '@/components/ui/table': {
      Table: passthrough('table'),
      TableHeader: passthrough('thead'),
      TableHead: passthrough('th'),
      TableBody: passthrough('tbody'),
      TableRow: passthrough('tr'),
      TableCell: passthrough('td'),
    },
    './score-config.css': {},
    '../../types/scoreConfig.types': scoreConfigTypes,
  });
}

test('ScoreComponentTable renders edit and delete buttons for ALL components when editable and hides them when published', () => {
  const { ScoreComponentTable } = loadScoreComponentTable();
  const components = scoreConfigTypes.UIT_INTEGER_PRESET_COMPONENTS.slice(0, 3);
  const edited = [];
  const deleted = [];

  const editableTree = ScoreComponentTable({
    components,
    calculationMethod: 'IntegerCoefficient',
    onChangeComponents: () => {},
    isReadOnly: false,
    configStatus: 'Draft',
    onAddComponent: () => {},
    onEditComponent: (comp, idx) => edited.push([comp.componentCode, idx]),
    onDeleteComponent: (comp, idx) => deleted.push([comp.componentCode, idx]),
  });

  const editButtons = collectElements(
    editableTree,
    (el) =>
      el.type === 'button' &&
      typeof el.props['aria-label'] === 'string' &&
      el.props['aria-label'].startsWith('Chỉnh sửa '),
  );
  const deleteButtons = collectElements(
    editableTree,
    (el) =>
      el.type === 'button' &&
      typeof el.props['aria-label'] === 'string' &&
      el.props['aria-label'].startsWith('Xóa '),
  );

  // Every preset row (ADVISOR, REVIEWER, COUNCIL_CHAIRMAN) must have both Edit and Delete buttons
  assert.equal(editButtons.length, 3);
  assert.equal(deleteButtons.length, 3);
  assert.equal(deleteButtons[0].props.disabled, false);

  editButtons[0].props.onClick();
  deleteButtons[1].props.onClick();
  assert.deepEqual(edited, [['ADVISOR', 0]]);
  assert.deepEqual(deleted, [['REVIEWER', 1]]);

  // When only 1 component remains in a saved Draft, delete button must be disabled
  const singleDraftTree = ScoreComponentTable({
    components: [components[0]],
    calculationMethod: 'IntegerCoefficient',
    onChangeComponents: () => {},
    isReadOnly: false,
    configStatus: 'Draft',
    onEditComponent: () => {},
    onDeleteComponent: () => {},
  });
  const singleDeleteButtons = collectElements(
    singleDraftTree,
    (el) =>
      el.type === 'button' &&
      typeof el.props['aria-label'] === 'string' &&
      el.props['aria-label'].startsWith('Xóa '),
  );
  assert.equal(singleDeleteButtons.length, 1);
  assert.equal(singleDeleteButtons[0].props.disabled, true);

  // When Active (published) and read-only, no Edit/Delete/Add buttons are rendered
  const lockedTree = ScoreComponentTable({
    components,
    calculationMethod: 'IntegerCoefficient',
    onChangeComponents: () => {},
    isReadOnly: true,
    isLoading: false,
    configStatus: 'Active',
    onAddComponent: () => {},
    onEditComponent: () => {},
    onDeleteComponent: () => {},
  });

  assert.equal(
    collectElements(
      lockedTree,
      (el) =>
        el.type === 'button' &&
        typeof el.props['aria-label'] === 'string' &&
        el.props['aria-label'].startsWith('Chỉnh sửa '),
    ).length,
    0,
  );
  assert.equal(
    collectElements(
      lockedTree,
      (el) =>
        el.type === 'button' &&
        typeof el.props['aria-label'] === 'string' &&
        el.props['aria-label'].startsWith('Xóa '),
    ).length,
    0,
  );
  const statusBadges = collectElements(
    lockedTree,
    (el) => el.props?.role === 'status' && el.props?.children === 'Đã công bố (chỉ đọc)',
  );
  assert.equal(statusBadges.length, 1);
});

test('ScoreComponentAddDialog supports edit mode: keeps own code allowed, preserves orderIndex, and blocks duplicate of other codes', () => {
  const { reactMock, render } = createHookRunner();
  const Dialog = ({ title, children }) => React.createElement('div', { 'data-title': title }, children);
  const passthrough =
    (tag) =>
    ({ children, ...props }) =>
      React.createElement(tag, props, children);

  const { ScoreComponentAddDialog } = loadTsModule(
    'features/academic/components/score-config/ScoreComponentAddDialog.tsx',
    {
      react: { default: React, ...React, ...reactMock },
      'react/jsx-runtime': jsxRuntime,
      '@/components/ui/dialog': { Dialog },
      '@/components/ui/button': { Button: passthrough('button') },
      '@/components/ui/input': { Input: passthrough('input') },
      '@/components/ui/select': { Select: passthrough('select') },
      '@/components/ui/checkbox': { Checkbox: passthrough('input') },
      '@/components/ui/form-error-summary': { FormErrorSummary: () => null },
      '../../types/scoreConfig.types': scoreConfigTypes,
    },
  );

  const original = scoreConfigTypes.UIT_INTEGER_PRESET_COMPONENTS[1]; // REVIEWER, orderIndex: 2
  const saved = [];
  let closedCount = 0;

  const dialogTree = ScoreComponentAddDialog({
    open: true,
    onClose: () => {
      closedCount++;
    },
    onSave: (comp) => saved.push(comp),
    initialComponent: original,
    calculationMethod: 'IntegerCoefficient',
    existingCodes: ['ADVISOR', 'REVIEWER', 'COUNCIL_CHAIRMAN'],
    nextOrderIndex: 4,
  });

  assert.equal(dialogTree.props.title, 'Chỉnh sửa đầu điểm');

  // Render the inner keyed form component directly via our hook runner
  const formElement = dialogTree.props.children;
  const renderedForm = render(() => formElement.type(formElement.props));
  const nameInput = collectElements(
    renderedForm,
    (el) => el.props?.id === 'score-component-name',
  )[0];
  nameInput.props.onChange({ target: { value: 'Điểm phản biện chỉnh sửa' } });

  const updatedForm = render(() => formElement.type(formElement.props));
  const formTag = collectElements(updatedForm, (el) => el.type === 'form')[0];
  formTag.props.onSubmit({ preventDefault() {} });

  assert.equal(saved.length, 1);
  assert.equal(saved[0].componentCode, 'REVIEWER');
  assert.equal(saved[0].componentName, 'Điểm phản biện chỉnh sửa');
  assert.equal(saved[0].orderIndex, 2); // Preserved original orderIndex
  assert.equal(closedCount, 1);
});

test('useScoreConfigActions supports deleting a component from a Draft via updateDraft and blocks editing Active configs', async () => {
  const { reactMock, render } = createHookRunner();
  const updateCalls = [];
  const createCalls = [];
  const errors = [];
  const successes = [];

  const { useScoreConfigActions } = loadTsModule(
    'features/academic/hooks/score-config/useScoreConfigActions.ts',
    {
      react: reactMock,
      '../../services/scoreConfigApi': {
        scoreConfigApi: {
          createDraft: async (batchId, payload) => {
            createCalls.push([batchId, payload]);
            return { id: 'new-draft', batchId, status: 'Draft', versionNo: 1, ...payload };
          },
          updateDraft: async (batchId, configId, payload) => {
            updateCalls.push([batchId, configId, payload]);
            return { id: configId, batchId, status: 'Draft', versionNo: 1, ...payload };
          },
          publishConfiguration: async () => ({}),
        },
        getScoreConfigErrorMessage: (err) => err?.message ?? 'Error',
      },
      '../../utils/scoreWeights': scoreWeights,
    },
  );

  const draftConfig = {
    id: 'draft-1',
    batchId: 'batch-1',
    versionNo: 1,
    status: 'Draft',
    calculationMethod: 'Percentage',
    changeReason: null,
    components: scoreConfigTypes.UIT_PERCENTAGE_PRESET_COMPONENTS,
  };

  // 1. Delete 1 component from Draft -> calls updateDraft with remaining 2 components (total 70%)
  const hook = render(() => useScoreConfigActions());
  const remaining = scoreConfigTypes.UIT_PERCENTAGE_PRESET_COMPONENTS.slice(0, 2);
  const ok = await hook.deleteComponentFromDraft({
    selectedBatchId: 'batch-1',
    currentConfig: draftConfig,
    isLoadingConfig: false,
    calculationMethod: 'Percentage',
    nextComponents: remaining,
    onSuccess: (saved) => successes.push(saved),
    onError: (msg) => errors.push(msg),
  });

  assert.equal(ok, true);
  assert.equal(updateCalls.length, 1);
  assert.equal(updateCalls[0][1], 'draft-1');
  assert.equal(updateCalls[0][2].components.length, 2);
  assert.equal(successes.length, 1);

  // 2. Deleting last component (0 remaining) is blocked client-side before calling API
  const emptyOk = await hook.deleteComponentFromDraft({
    selectedBatchId: 'batch-1',
    currentConfig: draftConfig,
    isLoadingConfig: false,
    calculationMethod: 'Percentage',
    nextComponents: [],
    onSuccess: () => {},
    onError: (msg) => errors.push(msg),
  });
  assert.equal(emptyOk, false);
  assert.equal(updateCalls.length, 1);

  // 3. Attempting to save an Active (published) config is rejected without calling API
  await hook.executeSaveDraft({
    selectedBatchId: 'batch-1',
    currentConfig: { ...draftConfig, status: 'Active' },
    currentBatch: null,
    isLoadingConfig: false,
    calculationMethod: 'Percentage',
    components: scoreConfigTypes.UIT_PERCENTAGE_PRESET_COMPONENTS,
    isValid: true,
    totalWeight: 100,
    onSuccess: () => {},
    onError: (msg) => errors.push(msg),
  });
  assert.equal(createCalls.length, 0);
  assert.equal(updateCalls.length, 1);
  assert.ok(errors.some((msg) => msg.includes('không thể chỉnh sửa')));

  // 4. Creating a brand-new config with 0 components is blocked
  await hook.executeSaveDraft({
    selectedBatchId: 'batch-1',
    currentConfig: null,
    currentBatch: null,
    isLoadingConfig: false,
    calculationMethod: 'IntegerCoefficient',
    components: [],
    isValid: false,
    totalWeight: 0,
    onSuccess: () => {},
    onError: (msg) => errors.push(msg),
  });
  assert.equal(createCalls.length, 0);
  assert.ok(errors.some((msg) => msg.includes('ít nhất 1 đầu điểm')));
});
