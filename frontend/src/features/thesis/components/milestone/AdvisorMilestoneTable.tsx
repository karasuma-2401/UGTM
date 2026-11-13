import { useMemo } from 'react';
import { CalendarClock, Flag } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { stripRichText } from '@/components/ui/rich-text-content';
import { cn } from '@/lib/utils';
import type { MilestoneWithReportDto } from '../../types/milestone.types';
import {
  formatMilestoneDateShort,
  formatMilestoneDateTime,
  formatMilestoneTimeShort,
  getDeadlineInfo,
  toReportStatus,
} from '../../utils/milestoneFormatters';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';

export interface AdvisorMilestoneTableProps {
  milestones: readonly MilestoneWithReportDto[];
  selectedMilestoneId: string | null;
  onSelectMilestone: (item: MilestoneWithReportDto) => void;
  onOpenReview: (milestoneId: string) => void;
  loading?: boolean;
  busy?: boolean;
  error?: string | null;
  onRetry?: () => void;
  hasActiveFilter?: boolean;
  onClearFilter?: () => void;
  canCreate?: boolean;
  onOpenCreate?: () => void;
}

const DEADLINE_TONE_CLASS = {
  completed: 'text-emerald-700',
  overdue: 'font-semibold text-rose-700',
  soon: 'font-medium text-amber-700',
  normal: 'text-slate-500',
} as const;

export function AdvisorMilestoneTable({
  milestones,
  selectedMilestoneId,
  onSelectMilestone,
  onOpenReview,
  loading = false,
  busy = false,
  error = null,
  onRetry,
  hasActiveFilter = false,
  onClearFilter,
  canCreate = false,
  onOpenCreate,
}: AdvisorMilestoneTableProps) {
  const columns = useMemo<DataTableColumn<MilestoneWithReportDto>[]>(
    () => [
      {
        id: 'order',
        header: 'STT',
        widthClassName: 'w-14',
        align: 'center',
        headerClassName: 'px-3 py-3.5 whitespace-nowrap',
        cellClassName: 'px-3 py-3',
        skeleton: { variant: 'number' },
        cell: (_, index) => (
          <span className="text-xs font-medium tabular-nums text-slate-400">
            {index + 1}
          </span>
        ),
      },
      {
        id: 'title',
        header: 'Mốc tiến độ',
        headerClassName: 'px-4 py-3.5',
        cellClassName: 'px-4 py-3',
        skeleton: { variant: 'topic' },
        cell: (item) => {
          const summary = stripRichText(item.milestone.description);
          return (
            <div className="min-w-0 space-y-1">
              <p
                className="line-clamp-2 text-sm font-semibold leading-snug text-slate-900"
                title={item.milestone.title}
              >
                {item.milestone.title}
              </p>
              <p
                className={cn(
                  'truncate text-xs font-normal',
                  summary ? 'text-slate-500' : 'text-slate-400 italic',
                )}
                title={summary || undefined}
              >
                {summary || 'Chưa có mô tả yêu cầu chi tiết'}
              </p>
            </div>
          );
        },
      },
      {
        id: 'deadline',
        header: 'Hạn nộp',
        widthClassName: 'w-40',
        headerClassName: 'px-4 py-3.5 whitespace-nowrap',
        cellClassName: 'px-4 py-3',
        skeleton: { variant: 'text' },
        cell: (item) => {
          const status = toReportStatus(item.report?.currentStatus);
          const deadlineInfo = getDeadlineInfo(item.milestone.deadlineUtc, status);
          return (
            <div className="min-w-0 space-y-0.5">
              <p className="whitespace-nowrap text-xs font-medium tabular-nums text-slate-800">
                {formatMilestoneDateShort(item.milestone.deadlineUtc)} ·{' '}
                {formatMilestoneTimeShort(item.milestone.deadlineUtc)}
              </p>
              <p className={cn('text-xs', DEADLINE_TONE_CLASS[deadlineInfo.urgency])}>
                {deadlineInfo.label}
              </p>
            </div>
          );
        },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        widthClassName: 'w-40',
        headerClassName: 'px-4 py-3.5 whitespace-nowrap',
        cellClassName: 'px-4 py-3',
        skeleton: { variant: 'badge' },
        cell: (item) => {
          const status = toReportStatus(item.report?.currentStatus);
          const latestVersion = item.report?.versions?.at(-1) ?? null;
          return (
            <div className="min-w-0 space-y-1">
              <MilestoneStatusBadge status={status} />
              <p className="truncate text-xs text-slate-500">
                {item.report && item.report.currentVersionNo > 0
                  ? `Phiên bản v${item.report.currentVersionNo}${latestVersion?.isLate ? ' (Trễ hạn)' : ''}`
                  : 'Chưa có bài nộp'}
              </p>
            </div>
          );
        },
      },
      {
        id: 'actions',
        header: 'Thao tác',
        align: 'right',
        widthClassName: 'w-28',
        headerClassName: 'px-4 py-3.5 whitespace-nowrap',
        cellClassName: 'px-4 py-3 text-right',
        skeleton: { variant: 'actions', actionCount: 1 },
        cell: (item) => {
          const status = toReportStatus(item.report?.currentStatus);
          const pendingReview = status === 'Submitted';
          return (
            <div className="flex justify-end">
              {pendingReview ? (
                <Button
                  type="button"
                  size="sm"
                  onClick={(event) => {
                    event.stopPropagation();
                    onOpenReview(item.milestone.id);
                  }}
                  className="h-8 rounded-md px-3 text-xs font-medium shadow-2xs"
                >
                  Đánh giá
                </Button>
              ) : (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={(event) => {
                    event.stopPropagation();
                    onOpenReview(item.milestone.id);
                  }}
                  className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-2xs hover:bg-slate-50"
                >
                  Xem
                </Button>
              )}
            </div>
          );
        },
      },
    ],
    [onOpenReview],
  );

  const emptyConfig = hasActiveFilter
    ? {
        icon: <Flag className="size-8" aria-hidden="true" />,
        title: 'Không có mốc tiến độ khớp bộ lọc',
        description: 'Thử chọn trạng thái khác để xem toàn bộ tiến độ của nhóm.',
        actionLabel: 'Hiển thị tất cả',
        onAction: onClearFilter,
      }
    : {
        icon: <Flag className="size-8" aria-hidden="true" />,
        title: 'Chưa có mốc tiến độ nào',
        description: canCreate
          ? 'Thiết lập mốc đầu tiên để định hướng các đợt nộp báo cáo của nhóm.'
          : 'Mốc tiến độ sẽ hiển thị tại đây khi nhóm đủ điều kiện và được giao mốc.',
        actionLabel: canCreate ? 'Thêm mốc đầu tiên' : undefined,
        onAction: canCreate ? onOpenCreate : undefined,
      };

  return (
    <DataTable
      data={[...milestones]}
      columns={columns}
      getRowId={(item) => item.milestone.id}
      ariaLabel="Danh sách mốc tiến độ sinh viên"
      selectedRowId={selectedMilestoneId}
      onRowSelect={(_id, item) => onSelectMilestone(item)}
      onRowOpen={(item) => onOpenReview(item.milestone.id)}
      rowClassName="h-[68px]"
      loading={loading}
      busy={busy}
      error={error ? { message: error, onRetry } : null}
      empty={emptyConfig}
      renderMobileItem={(item) => {
        const status = toReportStatus(item.report?.currentStatus);
        const deadlineInfo = getDeadlineInfo(item.milestone.deadlineUtc, status);
        const summary = stripRichText(item.milestone.description);
        const selected = item.milestone.id === selectedMilestoneId;
        return (
          <div
            className={cn(
              'space-y-3 rounded-xl border p-4 transition-colors',
              selected ? 'border-primary/40 bg-blue-50/35' : 'border-slate-200 bg-white',
            )}
            onClick={() => onSelectMilestone(item)}
          >
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0 space-y-1">
                <p className="text-sm font-semibold text-slate-900">{item.milestone.title}</p>
                {summary && <p className="line-clamp-2 text-xs text-slate-600">{summary}</p>}
              </div>
              <MilestoneStatusBadge status={status} />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 pt-2.5 text-xs">
              <div className="space-y-0.5">
                <span className="inline-flex items-center gap-1 text-slate-600">
                  <CalendarClock className="size-3.5" aria-hidden="true" />
                  <span>{formatMilestoneDateTime(item.milestone.deadlineUtc)}</span>
                </span>
                <p className={DEADLINE_TONE_CLASS[deadlineInfo.urgency]}>{deadlineInfo.label}</p>
              </div>

              <Button
                type="button"
                variant={status === 'Submitted' ? 'default' : 'outline'}
                size="sm"
                onClick={(event) => {
                  event.stopPropagation();
                  onOpenReview(item.milestone.id);
                }}
                className="h-8 rounded-md px-3 text-xs font-medium"
              >
                {status === 'Submitted' ? 'Đánh giá' : 'Xem'}
              </Button>
            </div>
          </div>
        );
      }}
    />
  );
}
