import { cn } from '@/lib/utils';
import { Skeleton } from './skeleton';

interface SkeletonColumn {
  label: string;
  width?: string;
  variant?: 'text' | 'number' | 'checkbox' | 'topic' | 'badge' | 'actions';
  actionCount?: number;
}

interface TableSkeletonProps {
  columns: SkeletonColumn[];
  rows?: number;
  className?: string;
}

function CellSkeleton({ column }: { column: SkeletonColumn }) {
  switch (column.variant) {
    case 'number':
      return <Skeleton className="mx-auto h-3 w-5" />;
    case 'checkbox':
      return <Skeleton className="size-4 rounded-sm" />;
    case 'topic':
      return (
        <div className="flex items-center gap-3">
          <Skeleton className="size-9 shrink-0 rounded-lg" />
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-4 w-5/6" />
            <Skeleton className="h-3 w-2/3" />
          </div>
        </div>
      );
    case 'badge':
      return <Skeleton className="h-6 w-28 max-w-full" />;
    case 'actions':
      return (
        <div className="flex flex-wrap gap-2">
          {Array.from({ length: column.actionCount ?? 1 }, (_, index) => (
            <Skeleton key={index} className="h-9 w-24 max-w-full" />
          ))}
        </div>
      );
    default:
      return <Skeleton className="h-4 w-24 max-w-full" />;
  }
}

export function TableSkeleton({ columns, rows = 6, className }: TableSkeletonProps) {
  return (
    <div role="status" aria-busy="true">
      <span className="sr-only">Đang tải danh sách...</span>
      <table aria-hidden="true" className={cn('w-full table-fixed text-left text-sm', className)}>
        <colgroup>
          {columns.map((column, index) => (
            <col key={index} className={column.width} />
          ))}
        </colgroup>
        <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
          <tr>
            {columns.map((column, index) => (
              <th
                key={index}
                scope="col"
                className={cn(
                  'px-4 py-4 font-bold',
                  column.variant === 'number' && 'px-3 text-center',
                )}
              >
                {column.variant === 'checkbox' ? (
                  <Skeleton className="size-4 rounded-sm" />
                ) : (
                  column.label
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {Array.from({ length: rows }, (_, rowIndex) => (
            <tr key={rowIndex} className="h-[88px]">
              {columns.map((column, columnIndex) => (
                <td
                  key={columnIndex}
                  className={cn('px-4 py-4', column.variant === 'number' && 'px-3')}
                >
                  <CellSkeleton column={column} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
