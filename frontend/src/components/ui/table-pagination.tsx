import { ChevronLeft, ChevronRight } from 'lucide-react';
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
      </nav>
    </div>
  );
}
