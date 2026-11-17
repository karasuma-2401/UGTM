import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';

const source = readFileSync(
  new URL('../../src/components/ui/data-table.tsx', import.meta.url),
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
const dependencies = {
  'react/jsx-runtime': jsxRuntime,
  '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' ') },
  './button': { Button: 'Button' },
  './selectable-table-row': { SelectableTableRow: 'SelectableTableRow' },
  './selection-indicator': { SelectionIndicator: 'SelectionIndicator' },
  './table': Object.fromEntries(
    ['Table', 'TableHeader', 'TableHead', 'TableBody', 'TableCell', 'TableEmpty'].map((name) => [
      name,
      name,
    ]),
  ),
  './table-pagination': { TablePagination: 'TablePagination' },
  './table-skeleton': { TableSkeleton: 'TableSkeleton' },
};
new Function('require', 'exports', outputText)((name) => {
  assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
  return dependencies[name];
}, exports);
export const { DataTable } = exports;
