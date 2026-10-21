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
}: TablePaginationProps) {
  const last = Math.min(pageNumber * pageSize, totalCount);
  const first = totalCount > 0 ? Math.min((pageNumber - 1) * pageSize + 1, last) : 0;
  const safePageSize = pageSize > 0 ? pageSize : 10;
  const totalPages = Math.max(1, Math.ceil(totalCount / safePageSize));

  const isFirstDisabled = pageNumber <= 1 || !hasPreviousPage || isLoading;
  const isLastDisabled = pageNumber >= totalPages || !hasNextPage || isLoading;

  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/60 px-5 py-4',
        className,
      )}
    >
      <p className="text-xs text-slate-500">
        Hiển thị {first}–{last} / {totalCount} {itemLabel}
      </p>
      <nav aria-label="Phân trang" className="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="h-8 w-8 p-0"
          disabled={isFirstDisabled}
          onClick={() => onPageChange(1)}
          title="Trang đầu"
          aria-label="Trang đầu"
        >
          <ChevronsLeft className="size-4" />
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          leftIcon={<ChevronLeft />}
          disabled={!hasPreviousPage || isLoading}
          onClick={() => onPageChange(pageNumber - 1)}
        >
          Trước
        </Button>
        <span
          aria-current="page"
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium tabular-nums text-slate-700"
        >
          Trang {pageNumber}
        </span>
        <Button
          type="button"
          variant="outline"
          size="sm"
          rightIcon={<ChevronRight />}
          disabled={!hasNextPage || isLoading}
          onClick={() => onPageChange(pageNumber + 1)}
        >
          Sau
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="h-8 w-8 p-0"
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
