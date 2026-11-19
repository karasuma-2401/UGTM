import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
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
  new Function('require', 'exports', outputText)((name) => {
    assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
    return { __esModule: true, ...dependencies[name] };
  }, exports);
  return exports;
}

const rosterExcel = loadModule('features/academic/utils/rosterExcel.ts');
const { EXCEL_XLSX_MIME, INVALID_EXCEL_FILE_MESSAGE, inspectDraggedExcelTransfer, isExcelXlsxFile } =
  rosterExcel;

test('isExcelXlsxFile only accepts .xlsx files (case-insensitive)', () => {
  assert.equal(isExcelXlsxFile({ name: 'danh-sach-sv.xlsx' }), true);
  assert.equal(isExcelXlsxFile({ name: 'ROSTER.XLSX' }), true);
  assert.equal(isExcelXlsxFile({ name: 'legacy.xls' }), false);
  assert.equal(isExcelXlsxFile({ name: 'students.csv' }), false);
  assert.equal(isExcelXlsxFile({ name: 'thesis.pdf' }), false);
  assert.equal(isExcelXlsxFile({ name: 'avatar.png' }), false);
  assert.equal(isExcelXlsxFile({ name: 'noextension' }), false);
});

test('inspectDraggedExcelTransfer distinguishes valid .xlsx/unknown MIME vs invalid file types', () => {
  assert.equal(
    inspectDraggedExcelTransfer({
      items: [{ kind: 'file', type: EXCEL_XLSX_MIME }],
      types: ['Files'],
    }),
    'valid',
  );
  assert.equal(
    inspectDraggedExcelTransfer({
      items: [{ kind: 'file', type: '' }],
      types: ['Files'],
    }),
    'valid',
  );
  assert.equal(
    inspectDraggedExcelTransfer({
      items: [{ kind: 'file', type: 'application/pdf' }],
      types: ['Files'],
    }),
    'invalid',
  );
  assert.equal(
    inspectDraggedExcelTransfer({
      items: [{ kind: 'file', type: 'application/vnd.ms-excel' }],
      types: ['Files'],
    }),
    'invalid',
  );
  assert.equal(
    inspectDraggedExcelTransfer({
      items: [{ kind: 'string', type: 'text/plain' }],
      types: ['text/plain'],
    }),
    'invalid',
  );
  assert.equal(inspectDraggedExcelTransfer(null), 'idle');
});

function createCardHarness(initialProps = {}) {
  const states = [];
  const refs = [];
  const toastErrors = [];
  const selectedFiles = [];
  let stateIdx = 0;
  let refIdx = 0;

  const mockReact = {
    ...React,
    useState(init) {
      const idx = stateIdx++;
      if (states[idx] === undefined) {
        states[idx] = typeof init === 'function' ? init() : init;
      }
      return [
        states[idx],
        (next) => {
          states[idx] = typeof next === 'function' ? next(states[idx]) : next;
        },
      ];
    },
    useRef(init) {
      const idx = refIdx++;
      if (!refs[idx]) refs[idx] = { current: init };
      return refs[idx];
    },
    useMemo(factory) {
      return factory();
    },
  };

  const { RosterConfigCard } = loadModule('features/academic/components/roster/RosterConfigCard.tsx', {
    react: mockReact,
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@/components/ui/button': { Button: 'button' },
    '@/components/ui/select': { Select: 'div' },
    '@/shared/notifications': {
      notify: {
        error: (message, options) => toastErrors.push({ message, options }),
      },
    },
    '../../utils/rosterExcel': rosterExcel,
  });

  function renderTree() {
    stateIdx = 0;
    refIdx = 0;
    return RosterConfigCard({
      activeRole: 'Student',
      onSwitchRole: () => {},
      batches: [],
      selectedBatchId: '',
      onSelectBatchId: () => {},
      fileInfo: null,
      onSelectFile: (file) => selectedFiles.push(file),
      onDownloadTemplate: () => {},
      isDownloading: false,
      onOpenSingleAdd: () => {},
      ...initialProps,
    });
  }

  function findDropzone(node) {
    if (!node || typeof node !== 'object') return null;
    if (node.props && 'data-drag-status' in node.props) return node;
    const children = node.props?.children;
    if (Array.isArray(children)) {
      for (const child of children) {
        const found = findDropzone(child);
        if (found) return found;
      }
    } else if (children) {
      return findDropzone(children);
    }
    return null;
  }

  return { renderTree, findDropzone, toastErrors, selectedFiles };
}

function makeDragEvent(dataTransfer) {
  let prevented = false;
  let stopped = false;
  return {
    dataTransfer,
    preventDefault() {
      prevented = true;
    },
    stopPropagation() {
      stopped = true;
    },
    get prevented() {
      return prevented;
    },
    get stopped() {
      return stopped;
    },
  };
}

test('RosterConfigCard highlights valid Excel drag and accepts dropped .xlsx file', () => {
  const { renderTree, findDropzone, toastErrors, selectedFiles } = createCardHarness();
  let zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'idle');

  const dt = {
    items: [{ kind: 'file', type: EXCEL_XLSX_MIME }],
    types: ['Files'],
    dropEffect: '',
    files: [{ name: 'sinh-vien.xlsx', size: 1024 }],
  };

  const enterEv = makeDragEvent(dt);
  zone.props.onDragEnter(enterEv);
  assert.equal(enterEv.prevented, true);
  zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'valid');

  const overEv = makeDragEvent(dt);
  zone.props.onDragOver(overEv);
  assert.equal(dt.dropEffect, 'copy');

  const dropEv = makeDragEvent(dt);
  zone.props.onDrop(dropEv);
  zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'idle');
  assert.equal(selectedFiles.length, 1);
  assert.equal(selectedFiles[0].name, 'sinh-vien.xlsx');
  assert.equal(toastErrors.length, 0);
});

test('RosterConfigCard blocks non-Excel files during dragover/drop and triggers error toast', () => {
  const { renderTree, findDropzone, toastErrors, selectedFiles } = createCardHarness({
    fileInfo: { name: 'existing.xlsx', sizeLabel: '12 KB', statusText: 'Đã kiểm tra định dạng' },
  });
  let zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'idle');

  const pdfDt = {
    items: [{ kind: 'file', type: 'application/pdf' }],
    types: ['Files'],
    dropEffect: '',
    files: [{ name: 'not-excel.pdf', size: 2048 }],
  };

  zone.props.onDragEnter(makeDragEvent(pdfDt));
  zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'invalid');

  zone.props.onDragOver(makeDragEvent(pdfDt));
  assert.equal(pdfDt.dropEffect, 'none');

  zone.props.onDrop(makeDragEvent(pdfDt));
  zone = findDropzone(renderTree());
  assert.equal(zone.props['data-drag-status'], 'idle');
  assert.equal(selectedFiles.length, 0);
  assert.equal(toastErrors.length, 1);
  assert.equal(toastErrors[0].message, INVALID_EXCEL_FILE_MESSAGE);
});
