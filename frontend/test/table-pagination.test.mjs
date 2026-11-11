import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as jsx from 'react/jsx-runtime';
const { outputText } = ts.transpileModule(
  readFileSync(new URL('../src/components/ui/table-pagination.tsx', import.meta.url), 'utf8'),
  {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      jsx: ts.JsxEmit.ReactJSX,
      target: ts.ScriptTarget.ES2022,
    },
  },
);
const exports = {};
new Function('require', 'exports', outputText)(
  (name) =>
    ({
      'react/jsx-runtime': jsx,
      './button': { Button: 'Button' },
      '@/lib/utils': { cn: (...args) => args.filter(Boolean).join(' ') },
      'lucide-react': Object.fromEntries(
        ['ChevronLeft', 'ChevronRight', 'ChevronsLeft', 'ChevronsRight'].map((name) => [
          name,
          name,
        ]),
      ),
    })[name],
  exports,
);
const { TablePagination } = exports;
function walk(node, predicate) {
  if (!node || typeof node !== 'object') return [];
  if (Array.isArray(node)) return node.flatMap((n) => walk(n, predicate));
  return [...(predicate(node) ? [node] : []), ...walk(node.props?.children, predicate)];
}
const render = (props = {}) =>
  TablePagination({ pageNumber: 1, pageSize: 10, totalCount: 200, onPageChange() {}, ...props });
const button = (tree, label) =>
  walk(tree, (n) => n.type === 'Button' && n.props['aria-label'] === label)[0];
test('page windows stay bounded and retain the current and endpoint pages', () => {
  for (const page of [1, 2, 3, 4, 50, 98, 99, 100]) {
    const tree = render({ pageNumber: page, totalCount: 1000 });
    const pages = walk(
      tree,
      (n) => n.type === 'Button' && /^Trang \d+$/.test(n.props['aria-label']),
    );
    assert.ok(pages.length <= 7);
    assert.ok(button(tree, 'Trang 1'));
    assert.ok(button(tree, 'Trang 100'));
    assert.equal(button(tree, `Trang ${page}`).props['aria-current'], 'page');
  }
});
test('navigation respects empty data, boundaries, server flags and loading', () => {
  const empty = render({ totalCount: 0 });
  assert.equal(button(empty, 'Trang trước').props.disabled, true);
  assert.equal(button(empty, 'Trang sau').props.disabled, true);
  assert.equal(button(render({ pageNumber: 20 }), 'Trang sau').props.disabled, true);
  assert.equal(button(render({ hasNextPage: false }), 'Trang sau').props.disabled, true);
  for (const b of walk(
    render({ isLoading: true, onPageSizeChange() {} }),
    (n) => n.type === 'Button',
  ))
    assert.equal(b.props.disabled, true);
  const changes = [];
  const tree = render({ pageNumber: 5, onPageChange: (p) => changes.push(p) });
  button(tree, 'Trang trước').props.onClick();
  button(tree, 'Trang sau').props.onClick();
  button(tree, 'Trang 1').props.onClick();
  button(tree, 'Trang 5').props.onClick();
  assert.deepEqual(changes, [4, 6, 1]);
});
test('page size controls preserve custom values and only notify actual changes', () => {
  const changes = [];
  const tree = render({ pageSize: 25, onPageSizeChange: (size) => changes.push(size) });
  assert.equal(button(tree, '25 hàng mỗi trang').props['aria-pressed'], true);
  button(tree, '25 hàng mỗi trang').props.onClick();
  button(tree, '50 hàng mỗi trang').props.onClick();
  assert.deepEqual(changes, [50]);
  assert.equal(walk(render(), (n) => n.props?.['aria-label'] === 'Số hàng mỗi trang').length, 0);
});
