import {
  forwardRef,
  type HTMLAttributes,
  type TdHTMLAttributes,
  type ThHTMLAttributes,
  type TableHTMLAttributes,
} from 'react';
import { ArrowUpDown, ArrowUp, ArrowDown, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from './button';
import { SelectableTableRow } from './selectable-table-row';

/* -------------------------------------------------------------------------------------------------
 * Table Root Container
 * -----------------------------------------------------------------------------------------------*/
export interface TableProps extends TableHTMLAttributes<HTMLTableElement> {
  containerClassName?: string;
}

export const Table = forwardRef<HTMLTableElement, TableProps>(
  ({ className, containerClassName, children, ...props }, ref) => (
    <div className={cn('relative w-full overflow-x-auto', containerClassName)}>
      <table
        ref={ref}
        className={cn('w-full caption-bottom text-sm text-left border-collapse', className)}
        {...props}
      >
        {children}
      </table>
    </div>
  ),
);
Table.displayName = 'Table';

/* -------------------------------------------------------------------------------------------------
 * Table Header
 * -----------------------------------------------------------------------------------------------*/
export interface TableHeaderProps extends HTMLAttributes<HTMLTableSectionElement> {
  sticky?: boolean;
}

export const TableHeader = forwardRef<HTMLTableSectionElement, TableHeaderProps>(
  ({ className, sticky = false, ...props }, ref) => (
    <thead
      ref={ref}
      className={cn(
        'border-b border-slate-200/80 bg-slate-50/90 text-xs text-slate-600 backdrop-blur-xs',
        sticky && 'sticky top-0 z-10',
        className,
      )}
      {...props}
    />
  ),
);
TableHeader.displayName = 'TableHeader';

/* -------------------------------------------------------------------------------------------------
 * Table Body
 * -----------------------------------------------------------------------------------------------*/
export const TableBody = forwardRef<
  HTMLTableSectionElement,
  HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tbody
    ref={ref}
    className={cn('divide-y divide-slate-100 [&_tr:last-child]:border-0', className)}
    {...props}
  />
));
TableBody.displayName = 'TableBody';

/* -------------------------------------------------------------------------------------------------
 * Table Footer
 * -----------------------------------------------------------------------------------------------*/
export const TableFooter = forwardRef<
  HTMLTableSectionElement,
  HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tfoot
    ref={ref}
    className={cn(
      'border-t border-slate-200/80 bg-slate-50/50 font-medium text-slate-600 [&>tr]:last:border-b-0',
      className,
    )}
    {...props}
  />
));
TableFooter.displayName = 'TableFooter';

/* -------------------------------------------------------------------------------------------------
 * Table Row
 * -----------------------------------------------------------------------------------------------*/
export interface TableRowProps extends HTMLAttributes<HTMLTableRowElement> {
  selected?: boolean;
  onSelect?: () => void;
  disabled?: boolean;
  hideSelectionMarker?: boolean;
}

// Backwards-compatible entry point; row interactions live in one implementation.
export const TableRow = forwardRef<HTMLTableRowElement, TableRowProps>((props, ref) => (
  <SelectableTableRow {...props} ref={ref} />
));
TableRow.displayName = 'TableRow';

/* -------------------------------------------------------------------------------------------------
 * Table Head Cell (th)
 * -----------------------------------------------------------------------------------------------*/
export type TableSortDirection = 'asc' | 'desc' | null;

export interface TableHeadProps extends ThHTMLAttributes<HTMLTableCellElement> {
  sortable?: boolean;
  sortDisabled?: boolean;
  sortDirection?: TableSortDirection;
  onSort?: () => void;
  align?: 'left' | 'center' | 'right';
}

export const TableHead = forwardRef<HTMLTableCellElement, TableHeadProps>(
  (
    {
      className,
      children,
      sortable = false,
      sortDisabled = false,
      sortDirection = null,
      onSort,
      align = 'left',
      ...props
    },
    ref,
  ) => {
    const alignClass =
      align === 'center' ? 'text-center' : align === 'right' ? 'text-right' : 'text-left';

    return (
      <th
        ref={ref}
        scope="col"
        aria-sort={
          sortDirection === 'asc' ? 'ascending' : sortDirection === 'desc' ? 'descending' : 'none'
        }
        className={cn(
          'h-11 px-4 py-3 align-middle text-xs font-semibold tracking-tight text-slate-700 select-none',
          alignClass,
          className,
        )}
        {...props}
      >
        {sortable ? (
          <button
            type="button"
            disabled={sortDisabled}
            onClick={onSort}
            className={cn(
              'group inline-flex items-center gap-1.5 rounded-md px-1.5 py-1 -mx-1.5 -my-1 font-semibold transition-colors hover:bg-slate-200/60 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-primary cursor-pointer disabled:cursor-wait disabled:opacity-50',
              sortDirection && 'text-primary font-bold',
            )}
            title={
              sortDirection === 'asc'
                ? 'Đang sắp xếp tăng dần. Nhấn để đổi thứ tự'
                : sortDirection === 'desc'
                  ? 'Đang sắp xếp giảm dần. Nhấn để đổi thứ tự'
                  : 'Nhấn để sắp xếp theo cột này'
            }
          >
            <span>{children}</span>
            <span className="shrink-0 text-slate-400 group-hover:text-slate-600 transition-colors">
              {sortDirection === 'asc' ? (
                <ArrowUp className="size-3.5 text-primary stroke-[2.5]" />
              ) : sortDirection === 'desc' ? (
                <ArrowDown className="size-3.5 text-primary stroke-[2.5]" />
              ) : (
                <ArrowUpDown className="size-3.5 opacity-60 group-hover:opacity-100" />
              )}
            </span>
          </button>
        ) : (
          children
        )}
      </th>
    );
  },
);
TableHead.displayName = 'TableHead';

/* -------------------------------------------------------------------------------------------------
 * Table Cell (td)
 * -----------------------------------------------------------------------------------------------*/
export interface TableCellProps extends TdHTMLAttributes<HTMLTableCellElement> {
  align?: 'left' | 'center' | 'right';
}

export const TableCell = forwardRef<HTMLTableCellElement, TableCellProps>(
  ({ className, align = 'left', ...props }, ref) => {
    const alignClass =
      align === 'center' ? 'text-center' : align === 'right' ? 'text-right' : 'text-left';

    return (
      <td
        ref={ref}
        className={cn(
          'px-4 py-3.5 sm:px-5 sm:py-4 align-middle text-slate-700',
          alignClass,
          className,
        )}
        {...props}
      />
    );
  },
);
TableCell.displayName = 'TableCell';

/* -------------------------------------------------------------------------------------------------
 * Table Caption
 * -----------------------------------------------------------------------------------------------*/
export const TableCaption = forwardRef<
  HTMLTableCaptionElement,
  HTMLAttributes<HTMLTableCaptionElement>
>(({ className, ...props }, ref) => (
  <caption ref={ref} className={cn('mt-4 text-xs text-slate-500', className)} {...props} />
));
TableCaption.displayName = 'TableCaption';

/* -------------------------------------------------------------------------------------------------
 * Table Empty State
 * -----------------------------------------------------------------------------------------------*/
export interface TableEmptyProps extends HTMLAttributes<HTMLDivElement> {
  icon?: React.ReactNode;
  title: string;
  description?: string;
  action?: React.ReactNode;
}

export function TableEmpty({
  icon,
  title,
  description,
  action,
  className,
  ...props
}: TableEmptyProps) {
  return (
    <div
      className={cn(
        'flex min-h-[18rem] flex-col items-center justify-center gap-3 p-8 text-center',
        className,
      )}
      {...props}
    >
      {icon && (
        <div className="flex size-12 items-center justify-center rounded-2xl bg-slate-100/80 text-slate-400">
          {icon}
        </div>
      )}
      <div className="space-y-1">
        <h4 className="text-sm font-semibold text-slate-900">{title}</h4>
        {description && <p className="text-xs text-slate-500 max-w-sm">{description}</p>}
      </div>
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

/* -------------------------------------------------------------------------------------------------
 * Table Active Filter Chips
 * -----------------------------------------------------------------------------------------------*/
export interface TableFilterChipProps {
  label: string;
  value: string;
  onRemove: () => void;
  className?: string;
}

export function TableFilterChip({ label, value, onRemove, className }: TableFilterChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-lg border border-slate-200/90 bg-slate-50/80 px-2.5 py-1 text-xs font-medium text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-100/80',
        className,
      )}
    >
      <span className="text-slate-500">{label}:</span>
      <span className="font-semibold text-slate-900">{value}</span>
      <button
        type="button"
        onClick={onRemove}
        aria-label={`Bỏ lọc ${label}`}
        className="rounded p-0.5 text-slate-400 hover:bg-slate-200/60 hover:text-slate-700 transition-colors cursor-pointer"
      >
        <X className="size-3" />
      </button>
    </span>
  );
}

export interface TableActiveFiltersProps {
  chips: Array<{ key: string; label: string; value: string; onRemove: () => void }>;
  onClearAll?: () => void;
  className?: string;
}

export function TableActiveFilters({ chips, onClearAll, className }: TableActiveFiltersProps) {
  if (chips.length === 0) return null;

  return (
    <div
      className={cn(
        'flex flex-wrap items-center gap-2 border-b border-slate-100 bg-slate-50/40 px-5 py-2.5',
        className,
      )}
    >
      <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400 mr-1">
        Bộ lọc đang bật:
      </span>
      {chips.map((chip) => (
        <TableFilterChip
          key={chip.key}
          label={chip.label}
          value={chip.value}
          onRemove={chip.onRemove}
        />
      ))}
      {onClearAll && chips.length > 1 && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={onClearAll}
          className="h-7 text-xs text-slate-500 hover:text-slate-800 cursor-pointer"
        >
          Xóa tất cả
        </Button>
      )}
    </div>
  );
}
