import assert from 'node:assert/strict';
import test from 'node:test';
import * as React from 'react';
import { DataTable } from './helpers/data-table.mjs';

const data = [
  { id: 'z', name: 'Zeta' },
  { id: 'a', name: 'Alpha' },
];
const columns = [
  {
    id: 'name',
    header: 'Tên',
    sortKey: 'name',
    cell: (row) => row.name,
    skeleton: { variant: 'person' },
  },
];
const base = { data, columns, getRowId: (row) => row.id, ariaLabel: 'Danh sách thử nghiệm' };
function nodes(tree) {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  return React.isValidElement(tree) ? [tree, ...nodes(tree.props.children)] : [];
}
const all = (tree, type) => nodes(tree).filter((node) => node.type === type);

test('DataTable renders custom columns in server order and delegates sorting without mutating its input', () => {
  const sorted = [];
  const tree = DataTable({
    ...base,
    sort: { field: 'name', direction: 'asc' },
    onSortChange: (key) => sorted.push(key),
  });
  assert.deepEqual(
    all(tree, 'SelectableTableRow').map((row) => row.key),
    ['z', 'a'],
  );
  assert.equal(all(tree, 'TableCell')[0].props.children, 'Zeta');
  const header = all(tree, 'TableHead')[0];
  assert.equal(header.props.sortDirection, 'asc');
  header.props.onSort();
  assert.deepEqual(sorted, ['name']);
  assert.deepEqual(
    data.map((row) => row.id),
    ['z', 'a'],
  );
});

test('DataTable delegates selection and double-click and isolates nested actions', () => {
  const selected = [],
    opened = [];
  const tree = DataTable({
    ...base,
    selectedRowId: 'a',
    isRowSelected: (item) => item.id === 'z' || item.id === 'a',
    onRowSelect: (id) => selected.push(id),
    onRowOpen: (row) => opened.push(row.id),
  });
  const firstRow = all(tree, 'SelectableTableRow')[0];
  const row = all(tree, 'SelectableTableRow')[1];
  assert.equal(firstRow.props.selected, true);
  assert.equal(firstRow.props['data-active-row'], undefined);
  assert.equal(row.props.selected, true);
  assert.equal(row.props['data-active-row'], 'true');
  assert.equal(row.props.hideSelectionMarker, true);
  assert.equal(all(tree, 'SelectionIndicator')[0].props.activeKey, 'a');
  assert.equal(all(tree, 'SelectionIndicator')[0].props.selector, 'tr[data-active-row="true"]');
  row.props.onSelect();
  assert.deepEqual(selected, ['a']);
  assert.deepEqual(opened, []);
  row.props.onDoubleClick({ target: { closest: () => ({}) } });
  assert.deepEqual(opened, []);
  row.props.onDoubleClick({ target: { closest: () => null } });
  assert.deepEqual(opened, ['a']);
  assert.equal(all(DataTable(base), 'SelectionIndicator').length, 0);
});

test('busy tables lock sort, row opening and pagination while keeping the current rows visible', () => {
  let actions = 0;
  const tree = DataTable({
    ...base,
    busy: true,
    onSortChange: () => actions++,
    onRowOpen: () => actions++,
    pagination: { totalCount: 20 },
  });
  assert.equal(all(tree, 'SelectableTableRow')[0].props.disabled, true);
  all(tree, 'SelectableTableRow')[0].props.onDoubleClick({ target: { closest: () => null } });
  all(tree, 'TableHead')[0].props.onSort();
  assert.equal(all(tree, 'TableHead')[0].props.sortDisabled, true);
  assert.equal(all(tree, 'TablePagination')[0].props.isLoading, true);
  assert.equal(actions, 0);
});

test('errors hide cached rows and counts, expose retry, and take priority over loading', () => {
  let retries = 0;
  const tree = DataTable({
    ...base,
    loading: true,
    error: { message: 'Tải thất bại', onRetry: () => retries++ },
    pagination: { totalCount: 20 },
  });
  assert.equal(all(tree, 'SelectableTableRow').length, 0);
  assert.equal(all(tree, 'TablePagination').length, 0);
  assert.equal(all(tree, 'TableSkeleton').length, 0);
  all(tree, 'Button')[0].props.onClick();
  assert.equal(retries, 1);
});

test('loading uses column skeletons and empty pages retain pagination and a custom recovery action', () => {
  const loading = DataTable({ ...base, loading: true });
  assert.equal(all(loading, 'TableSkeleton')[0].props.columns[0].variant, 'person');
  assert.equal(all(loading, 'SelectableTableRow').length, 0);
  const action = React.createElement('button', null, 'Về trang đầu');
  const empty = DataTable({
    ...base,
    data: [],
    empty: { title: 'Trang trống', action },
    pagination: { totalCount: 20 },
  });
  assert.equal(all(empty, 'TableEmpty')[0].props.action, action);
  assert.equal(all(empty, 'TablePagination').length, 1);
});

test('mobile content is supplied by the caller while desktop keeps semantic table markup', () => {
  const tree = DataTable({
    ...base,
    renderMobileItem: (row) => React.createElement('p', null, row.name),
  });
  assert.equal(all(tree, 'Table')[0].props['aria-label'], base.ariaLabel);
  assert.equal(all(tree, 'ul')[0].props['aria-label'], base.ariaLabel);
  assert.deepEqual(
    all(tree, 'li').map((row) => row.key),
    ['z', 'a'],
  );
  assert.equal(all(tree, 'p')[1].props.children, 'Alpha');
});
