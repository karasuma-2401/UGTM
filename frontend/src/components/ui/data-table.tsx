import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { Button } from './button';
import { SelectableTableRow } from './selectable-table-row';
import { SelectionIndicator } from './selection-indicator';
import {
  Table,
  TableHeader,
  TableHead,
  TableBody,
  TableCell,
  TableEmpty,
  type TableEmptyProps,
} from './table';
import { TablePagination, type TablePaginationProps } from './table-pagination';
import { TableSkeleton } from './table-skeleton';

export interface DataTableColumn<T, SortKey extends string = string> {
  id: string;
  header: ReactNode;
  cell: (row: T, index: number) => ReactNode;
  widthClassName?: string;
  headerClassName?: string;
  cellClassName?: string;
  align?: 'left' | 'center' | 'right';
  sortKey?: SortKey;
  title?: string;
  skeleton?: {
    label?: string;
    variant?: 'text' | 'number' | 'checkbox' | 'topic' | 'person' | 'badge' | 'actions';
    actionCount?: number;
  };
}

export interface DataTableProps<T, SortKey extends string = string> {
  data: readonly T[];
  columns: readonly DataTableColumn<T, SortKey>[];
  getRowId: (row: T) => string;
  ariaLabel: string;
  selectedRowId?: string | null;
  onRowSelect?: (id: string, row: T) => void;
  onRowOpen?: (row: T) => void;
  isRowSelected?: (row: T) => boolean;
  rowClassName?: string;
  tableClassName?: string;
  className?: string;
  sort?: { field: SortKey; direction: 'asc' | 'desc' };
  onSortChange?: (field: SortKey) => void;
  loading?: boolean;
  busy?: boolean;
  error?: { message: string; onRetry?: () => void } | null;
  empty?: TableEmptyProps;
  pagination?: TablePaginationProps;
  renderMobileItem?: (row: T, index: number) => ReactNode;
}

/** Presentation only: callers own fetching, filtering, sorting and pagination. */
export function DataTable<T, SortKey extends string = string>({
  data,
  columns,
  getRowId,
  ariaLabel,
  selectedRowId,
  onRowSelect,
  onRowOpen,
  isRowSelected,
  rowClassName,
  tableClassName,
  className,
  sort,
  onSortChange,
  loading = false,
  busy = false,
  error,
  empty,
  pagination,
  renderMobileItem,
}: DataTableProps<T, SortKey>) {
  const disabled = busy || loading;
  return (
    <div className={className} aria-busy={disabled}>
      <div className={cn('transition-opacity', busy && !loading && 'opacity-60')}>
        {error ? (
          <div role="alert" className="space-y-3 p-6 text-sm text-rose-700">
            <p>{error.message}</p>
            {error.onRetry && (
              <Button variant="outline" onClick={error.onRetry}>
                Thử lại
              </Button>
            )}
          </div>
        ) : loading ? (
          <div className="overflow-x-auto">
            <TableSkeleton
              columns={columns.map((column) => ({
                label:
                  column.skeleton?.label ??
                  (typeof column.header === 'string' ? column.header : column.id),
                width: column.widthClassName,
                variant: column.skeleton?.variant,
                actionCount: column.skeleton?.actionCount,
              }))}
            />
          </div>
        ) : data.length === 0 ? (
          <TableEmpty title="Không có dữ liệu" {...empty} />
        ) : (
          <>
            <div className={cn('relative isolate', renderMobileItem && 'hidden lg:block')}>
              {onRowSelect && (
                <SelectionIndicator
                  activeKey={selectedRowId}
                  selector='tr[data-active-row="true"]'
                  variant="rail"
                  className="z-20 rounded-r bg-primary"
                />
              )}
              <Table className={cn('table-fixed', tableClassName)} aria-label={ariaLabel}>
                <colgroup>
                  {columns.map((column) => (
                    <col key={column.id} className={column.widthClassName} />
                  ))}
                </colgroup>
                <TableHeader>
                  <tr>
                    {columns.map((column) => (
                      <TableHead
                        key={column.id}
                        align={column.align}
                        className={column.headerClassName}
                        title={column.title}
                        sortable={Boolean(column.sortKey && onSortChange)}
                        sortDirection={
                          sort && column.sortKey === sort.field ? sort.direction : null
                        }
                        onSort={
                          column.sortKey && onSortChange
                            ? () => {
                                if (!disabled) onSortChange(column.sortKey!);
                              }
                            : undefined
                        }
                        sortDisabled={disabled}
                      >
                        {column.header}
                      </TableHead>
                    ))}
                  </tr>
                </TableHeader>
                <TableBody>
                  {data.map((row, index) => {
                    const id = getRowId(row);
                    return (
                      <SelectableTableRow
                        key={id}
                        data-selection-item
                        data-active-row={onRowSelect && selectedRowId === id ? 'true' : undefined}
                        hideSelectionMarker={Boolean(onRowSelect)}
                        selected={isRowSelected?.(row) ?? selectedRowId === id}
                        onSelect={onRowSelect ? () => onRowSelect(id, row) : undefined}
                        disabled={disabled}
                        className={rowClassName}
                        title={onRowOpen ? 'Nhấp đúp để xem chi tiết' : undefined}
                        onDoubleClick={
                          onRowOpen
                            ? (event) => {
                                if (
                                  disabled ||
                                  (event.target as Element).closest(
                                    'button, a, input, select, textarea, [role="button"], [role="checkbox"], label, [contenteditable="true"]',
                                  )
                                )
                                  return;
                                onRowOpen(row);
                              }
                            : undefined
                        }
                      >
                        {columns.map((column) => (
                          <TableCell
                            key={column.id}
                            align={column.align}
                            className={column.cellClassName}
                          >
                            {column.cell(row, index)}
                          </TableCell>
                        ))}
                      </SelectableTableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
            {renderMobileItem && (
              <ul className="divide-y divide-slate-200 lg:hidden" aria-label={ariaLabel}>
                {data.map((row, index) => (
                  <li key={getRowId(row)} className="space-y-3 p-4">
                    {renderMobileItem(row, index)}
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>
      {pagination && !loading && !error && (
        <TablePagination {...pagination} isLoading={disabled || pagination.isLoading} />
      )}
    </div>
  );
}
