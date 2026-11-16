import { useMemo } from 'react';
import { CheckCircle2, RotateCcw, Search, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import type { ArchivalFilterKey, ArchivalReviewRow } from '../../types/archival.types';
import type { ArchivalSortField, ArchivalSortState } from '../../utils/archival';

interface ArchivalReviewTableProps {
  items: ArchivalReviewRow[];
  totalCount: number;
  allRowsCount: number;
  selectedSubmissionId: string | null;
  onSelectRow: (submissionId: string) => void;
  sort: ArchivalSortState;
  onSortChange: (field: ArchivalSortField) => void;
  pageNumber: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  filterKey: ArchivalFilterKey;
  keyword: string;
  onKeywordChange: (val: string) => void;
  onResetFilters: () => void;
  onShowAll: () => void;
  isLoading?: boolean;
  isFetching?: boolean;
  listError?: string | null;
  onRetry?: () => void;
}

function formatStudentsInline(row: ArchivalReviewRow): string {
  if (row.studentNames.length === 0) return 'Chưa có thông tin sinh viên';
  return row.studentNames
    .map((name, idx) => (row.studentCodes[idx] ? `${name} (${row.studentCodes[idx]})` : name))
    .join(' · ');
}

export function ArchivalReviewTable({
  items,
  totalCount,
  allRowsCount,
  selectedSubmissionId,
  onSelectRow,
  sort,
  onSortChange,
  pageNumber,
  pageSize,
  onPageChange,
  onPageSizeChange,
  filterKey,
  keyword,
  onKeywordChange,
  onResetFilters,
  onShowAll,
  isLoading = false,
  isFetching = false,
  listError = null,
  onRetry,
}: ArchivalReviewTableProps) {
  const hasActiveFilters = filterKey !== 'AdvisorConfirmed' || keyword.trim().length > 0;

  const columns = useMemo<DataTableColumn<ArchivalReviewRow, ArchivalSortField>[]>(
    () => [
      {
        id: 'topicTitle',
        header: 'Đề tài & Nhóm',
        sortKey: 'topicTitle',
        headerClassName: 'px-3 sm:px-4',
        cellClassName: 'px-3 sm:px-4',
        skeleton: { variant: 'topic' },
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <div className="flex items-center gap-1.5 min-w-0">
              <span className="font-semibold text-primary tabular-nums shrink-0">
                {row.groupCode}
              </span>
              <span className="text-ink-faint shrink-0" aria-hidden="true">
                ·
              </span>
              <span
                className="font-semibold text-ink-primary truncate"
                title={row.topicTitleVi}
              >
                {row.topicTitleVi}
              </span>
            </div>
            <p className="text-xs text-ink-muted truncate">
              {row.advisorName ? `${row.advisorName} · ` : ''}
              {formatStudentsInline(row)}
            </p>
          </div>
        ),
      },
      {
        id: 'submittedAt',
        header: 'Phiên bản',
        sortKey: 'submittedAt',
        widthClassName: 'w-[160px]',
        headerClassName: 'px-2.5 sm:px-3 whitespace-nowrap',
        cellClassName: 'px-2.5 sm:px-3',
        skeleton: { variant: 'text' },
        cell: (row) => {
          const formattedDate = row.advisorConfirmedAt
            ? formatDateTimeVi(row.advisorConfirmedAt)
            : row.submittedAt
              ? formatDateTimeVi(row.submittedAt)
              : '—';
          const tooltip = row.advisorConfirmedAt
            ? `GVHD xác nhận: ${formattedDate}`
            : row.submittedAt
              ? `Nộp lúc: ${formattedDate}`
              : '—';
          return (
            <div className="min-w-0 space-y-0.5">
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-semibold text-ink-heading tabular-nums">
                  v{row.currentVersionNo}
                </span>
                {row.currentVersionNo > 1 && (
                  <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                    Nộp lại
                  </Badge>
                )}
              </div>
              <p
                className="flex items-center gap-1 text-[11px] text-ink-muted tabular-nums truncate"
                title={tooltip}
              >
                {row.advisorConfirmedAt && (
                  <CheckCircle2 className="size-3 text-tone-success-icon shrink-0" />
                )}
                <span className="truncate">{formattedDate}</span>
              </p>
            </div>
          );
        },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        sortKey: 'status',
        align: 'right',
        widthClassName: 'w-[178px]',
        headerClassName: 'px-3 sm:px-3.5 whitespace-nowrap',
        cellClassName: 'px-3 sm:px-3.5',
        skeleton: { variant: 'badge' },
        cell: (row) => (
          <div className="flex justify-end">
            <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>
          </div>
        ),
      },
    ],
    [],
  );

  return (
    <Card className="rounded-2xl border border-line/80 bg-surface-card shadow-xs overflow-hidden">
      <div className="p-4 sm:p-5 border-b border-line-subtle space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-ink-heading">
              Danh sách hồ sơ lưu chiểu
            </h2>
            <span className="inline-flex items-center rounded-full bg-surface-muted px-2 py-0.5 text-xs font-medium text-ink-secondary tabular-nums">
              {totalCount}
            </span>
          </div>
          <span className="text-xs text-ink-subtle hidden sm:inline">
            Chọn một hồ sơ để kiểm tra tệp và nghiệm thu
          </span>
        </div>

        <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2.5">
          <div className="relative flex-1 min-w-0">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
            <Input
              type="text"
              value={keyword}
              onChange={(e) => onKeywordChange(e.target.value)}
              placeholder="Tìm mã nhóm, tên đề tài, sinh viên hoặc GVHD..."
              aria-label="Tìm kiếm hồ sơ lưu chiểu"
              className="h-9 pl-9 pr-8 text-xs rounded-xl bg-surface-subtle/60 border-line-input focus-visible:bg-surface-card"
            />
            {keyword && (
              <button
                type="button"
                onClick={() => onKeywordChange('')}
                aria-label="Xóa từ khóa tìm kiếm"
                className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-ink-subtle hover:bg-surface-muted hover:text-ink-primary cursor-pointer"
              >
                <X className="size-3.5" />
              </button>
            )}
          </div>

          {hasActiveFilters && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onResetFilters}
              className="h-9 px-3 text-xs font-medium rounded-xl border-line-input text-ink-secondary hover:bg-surface-hover shrink-0"
            >
              <RotateCcw className="size-3.5 mr-1.5" />
              Đặt lại mặc định
            </Button>
          )}
        </div>
      </div>

      {listError && (
        <div className="p-4 border-b border-line-subtle">
          <InlineAlert
            tone="error"
            title="Không tải được danh sách hồ sơ"
            action={
              onRetry ? (
                <Button type="button" variant="outline" size="sm" onClick={onRetry}>
                  Thử lại
                </Button>
              ) : undefined
            }
          >
            {listError}
          </InlineAlert>
        </div>
      )}

      {isLoading && allRowsCount === 0 && (
        <ul className="divide-y divide-line lg:hidden" aria-hidden="true">
          {Array.from({ length: 4 }).map((_, index) => (
            <li key={index} className="p-4 space-y-2.5">
              <div className="flex items-center justify-between gap-2">
                <Skeleton className="h-4 w-20" />
                <Skeleton className="h-6 w-28 rounded-full" />
              </div>
              <Skeleton className="h-4 w-4/5" />
              <Skeleton className="h-3.5 w-3/5" />
            </li>
          ))}
        </ul>
      )}

      <div className={cn(isLoading && allRowsCount === 0 && 'hidden lg:block')}>
        <DataTable<ArchivalReviewRow, ArchivalSortField>
        ariaLabel="Danh sách hồ sơ lưu chiểu cần Khoa nghiệm thu"
        data={items}
        columns={columns}
        getRowId={(row) => row.submissionId}
        selectedRowId={selectedSubmissionId}
        onRowSelect={(id) => onSelectRow(id)}
        sort={sort}
        onSortChange={onSortChange}
        loading={isLoading && allRowsCount === 0}
        busy={isFetching && allRowsCount > 0}
        pagination={{
          pageNumber,
          pageSize,
          totalCount,
          onPageChange,
          onPageSizeChange,
          itemLabel: 'hồ sơ',
        }}
        empty={{
          title:
            allRowsCount === 0
              ? 'Chưa có hồ sơ lưu chiểu nào trong đợt'
              : 'Không có hồ sơ phù hợp bộ lọc',
          description:
            allRowsCount === 0
              ? 'Sinh viên cần nộp hồ sơ lưu chiểu sau khi có biên bản bảo vệ đã khóa.'
              : 'Thử chuyển sang "Tất cả hồ sơ" hoặc xóa từ khóa tìm kiếm.',
          action:
            filterKey !== 'ALL' || keyword ? (
              <Button type="button" variant="outline" size="sm" onClick={onShowAll}>
                Xem tất cả hồ sơ
              </Button>
            ) : undefined,
        }}
        renderMobileItem={(row) => {
          const isSelected = row.submissionId === selectedSubmissionId;
          return (
            <button
              type="button"
              onClick={() => onSelectRow(row.submissionId)}
              className={cn(
                'w-full text-left p-4 transition-colors space-y-2 cursor-pointer',
                isSelected ? 'bg-tone-info-bg/70' : 'hover:bg-surface-hover/60',
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-bold text-primary tabular-nums">
                    {row.groupCode}
                  </span>
                  <span className="text-xs font-semibold text-ink-heading tabular-nums">
                    v{row.currentVersionNo}
                  </span>
                  {row.currentVersionNo > 1 && (
                    <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                      Nộp lại
                    </Badge>
                  )}
                </div>
                <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>
              </div>

              <p className="text-sm font-semibold text-ink-primary leading-snug line-clamp-2">
                {row.topicTitleVi}
              </p>

              <p className="text-xs text-ink-muted line-clamp-1">
                {row.advisorName ? `GVHD: ${row.advisorName} · ` : ''}
                {formatStudentsInline(row)}
              </p>

              <div className="flex items-center justify-between text-[11px] text-ink-muted tabular-nums pt-0.5">
                <span>
                  {row.advisorConfirmedAt
                    ? `GVHD xác nhận: ${formatDateTimeVi(row.advisorConfirmedAt)}`
                    : row.submittedAt
                      ? `Nộp: ${formatDateTimeVi(row.submittedAt)}`
                      : '—'}
                </span>
                <span className="font-medium text-primary">
                  {isSelected ? 'Đang chọn' : 'Xem chi tiết'}
                </span>
              </div>
            </button>
          );
        }}
      />
      </div>
    </Card>
  );
}
