import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { Button } from './button';
import { cn } from '@/lib/utils';

export interface TablePaginationProps {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
  isLoading?: boolean;
  itemLabel?: string;
  className?: string;
  onPageSizeChange?: (size: number) => void;
}

export function TablePagination({
  pageNumber,
  pageSize,
  totalCount,
  onPageChange,
  hasPreviousPage = pageNumber > 1,
  hasNextPage = pageNumber * pageSize < totalCount,
  isLoading = false,
  itemLabel = 'bản ghi',
  className,
  onPageSizeChange,
}: TablePaginationProps) {
  const safePageSize = pageSize > 0 ? pageSize : 10;
  const totalPages = Math.max(1, Math.ceil(totalCount / safePageSize));
  const currentPage = Math.max(1, Math.min(pageNumber, totalPages));
  const last = Math.min(currentPage * safePageSize, totalCount);
  const first = totalCount > 0 ? (currentPage - 1) * safePageSize + 1 : 0;
  const sizes = [...new Set([10, 20, 50, safePageSize])].sort((a, b) => a - b);
  // Keep the control bounded even when a table has thousands of pages.
  const start = Math.max(2, Math.min(currentPage - 1, totalPages - 4));
  const end = Math.min(totalPages - 1, Math.max(currentPage + 1, 5));
  const pages: (number | 'start-gap' | 'end-gap')[] = [1];
  if (totalPages <= 7) {
    for (let page = 2; page <= totalPages; page++) pages.push(page);
  } else {
    if (start > 2) pages.push('start-gap');
    for (let page = start; page <= end; page++) pages.push(page);
    if (end < totalPages - 1) pages.push('end-gap');
    pages.push(totalPages);
  }

  const isFirstDisabled = currentPage <= 1 || !hasPreviousPage || isLoading;
  const isLastDisabled = currentPage >= totalPages || !hasNextPage || isLoading;
  const arrowClass = 'size-9 shrink-0 rounded-lg p-0 text-slate-500 disabled:opacity-35';

  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-x-6 gap-y-4 rounded-b-2xl border-t border-slate-100 bg-slate-50/50 px-4 py-4 sm:px-5',
        className,
      )}
    >
      <p className="w-full text-center text-xs leading-5 text-slate-600 sm:w-auto sm:text-left">
        <span className="text-slate-700 font-semibold tabular-nums">
          {first}–{last}
        </span>{' '}
        trên{' '}
        <span className="text-slate-700 font-semibold tabular-nums">
          {totalCount.toLocaleString('vi-VN')}
        </span>{' '}
        {itemLabel}
      </p>
      {onPageSizeChange && (
        <div className="flex w-full items-center justify-center gap-3 sm:ml-auto sm:w-auto">
          <span className="text-xs text-slate-600">Số hàng / trang</span>
          <div
            role="group"
            aria-label="Số hàng mỗi trang"
            className="inline-flex items-center gap-1 rounded-xl border border-slate-200/80 bg-slate-100/70 p-1"
          >
            {sizes.map((size) => (
              <Button
                key={size}
                type="button"
                variant="ghost"
                size="sm"
                aria-label={`${size} hàng mỗi trang`}
                aria-pressed={safePageSize === size}
                disabled={isLoading}
                onClick={() => {
                  if (size !== safePageSize) onPageSizeChange(size);
                }}
                className={cn(
                  'h-7 min-w-9 px-2 tabular-nums',
                  safePageSize === size
                    ? 'bg-white text-primary shadow-sm hover:bg-white hover:text-primary'
                    : 'text-slate-600 hover:bg-white/70',
                )}
              >
                {size}
              </Button>
            ))}
          </div>
        </div>
      )}
      <nav
        aria-label="Phân trang"
        className="flex w-full items-center justify-center gap-1 sm:w-auto"
      >
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={cn(arrowClass, 'sm:hidden')}
          disabled={isFirstDisabled}
          onClick={() => onPageChange(1)}
          title="Trang đầu"
          aria-label="Trang đầu"
        >
          <ChevronsLeft className="size-4" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={arrowClass}
          aria-label="Trang trước"
          title="Trang trước"
          disabled={isFirstDisabled}
          onClick={() => onPageChange(currentPage - 1)}
        >
          <ChevronLeft />
        </Button>
        <span className="min-w-24 px-2 text-center text-xs tabular-nums text-slate-600 sm:hidden">
          <span className="font-semibold text-primary">{currentPage}</span> / {totalPages}
        </span>
        <div className="hidden items-center gap-1 sm:flex">
          {pages.map((page) =>
            typeof page === 'number' ? (
              <Button
                key={page}
                type="button"
                variant={page === currentPage ? 'default' : 'ghost'}
                size="sm"
                aria-label={`Trang ${page}`}
                aria-current={page === currentPage ? 'page' : undefined}
                disabled={isLoading}
                onClick={() => {
                  if (page !== currentPage) onPageChange(page);
                }}
                className="h-9 min-w-9 px-2 tabular-nums"
              >
                {page}
              </Button>
            ) : (
              <span
                key={page}
                aria-hidden="true"
                className="w-7 text-center text-xs text-slate-400"
              >
                …
              </span>
            ),
          )}
        </div>
        <span className="sr-only" aria-live="polite" aria-atomic="true">
          Trang {currentPage} trên {totalPages}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={arrowClass}
          aria-label="Trang sau"
          title="Trang sau"
          disabled={isLastDisabled}
          onClick={() => onPageChange(currentPage + 1)}
        >
          <ChevronRight />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={cn(arrowClass, 'sm:hidden')}
          disabled={isLastDisabled}
          onClick={() => onPageChange(totalPages)}
          title="Trang cuối"
          aria-label="Trang cuối"
        >
          <ChevronsRight className="size-4" />
        </Button>
      </nav>
    </div>
  );
}
