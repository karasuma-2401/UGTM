import React from 'react';
import { AlertTriangle, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PeriodSummaryCardProps {
  academicYear: string;
  semester: number;
  batchName: string;
  status?: string;
  totalWeeks?: number;
  canEdit: boolean;
  onOpenBatch?: () => void;
  isOpenLoading?: boolean;
  activeOpenBatchName?: string | null;
}

const STATUS_MAP: Record<
  string,
  { label: string; bg: string; text: string; dot: string; ping: string }
> = {
  Draft: {
    label: 'Bản nháp',
    bg: 'bg-tone-warning-bg border-tone-warning-border',
    text: 'text-tone-warning-strong',
    dot: 'bg-amber-500',
    ping: 'bg-amber-400',
  },
  Open: {
    label: 'Đang hoạt động',
    bg: 'bg-tone-success-bg border-tone-success-border',
    text: 'text-tone-success-strong',
    dot: 'bg-emerald-500',
    ping: 'bg-emerald-400',
  },
  Closed: {
    label: 'Đã đóng',
    bg: 'bg-surface-muted border-line-strong',
    text: 'text-ink-heading',
    dot: 'bg-slate-500',
    ping: 'bg-slate-400',
  },
  Archived: {
    label: 'Đã lưu trữ',
    bg: 'bg-surface-muted border-line-strong',
    text: 'text-ink-heading',
    dot: 'bg-slate-500',
    ping: 'bg-slate-400',
  },
};

export const PeriodSummaryCard: React.FC<PeriodSummaryCardProps> = ({
  academicYear,
  semester,
  batchName,
  status = 'Draft',
  totalWeeks = 21,
  canEdit,
  onOpenBatch,
  isOpenLoading = false,
  activeOpenBatchName = null,
}) => {
  const currentStatus = STATUS_MAP[status] || STATUS_MAP.Draft;
  const isStandard21Weeks = totalWeeks === 21;
  const isBlockedByOtherOpenBatch = Boolean(activeOpenBatchName);

  return (
    <div className="rounded-2xl border border-line bg-surface-card p-6 shadow-xs transition-all hover:border-line-strong">
      <div className="mb-5 flex items-center justify-between gap-3">
        <h2 className="text-base font-bold text-ink-primary tracking-tight">
          Tình trạng đợt
        </h2>

        {/* Live Status Pill with clear visible border */}
        <div
          className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold whitespace-nowrap shrink-0 shadow-2xs ${currentStatus.bg} ${currentStatus.text}`}
        >
          <span className="relative flex size-2 shrink-0">
            {status === 'Open' && (
              <span
                className={`absolute inline-flex h-full w-full animate-ping rounded-full opacity-75 ${currentStatus.ping}`}
              />
            )}
            <span
              className={`relative inline-flex size-2 rounded-full ${currentStatus.dot}`}
            />
          </span>
          <span className="whitespace-nowrap">{currentStatus.label}</span>
        </div>
      </div>

      <div className="space-y-4">
        {/* KPI Metrics */}
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-xl border border-line bg-surface-subtle/70 p-3.5 transition-all">
            <span className="text-[11px] font-medium text-ink-muted block mb-0.5">
              Thời lượng chu kỳ
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-lg font-bold text-ink-primary">
                {totalWeeks}
              </span>
              <span className="text-xs text-ink-muted">tuần</span>
            </div>
            <span className="mt-1 inline-block text-[10px] text-ink-muted">
              {isStandard21Weeks ? 'Chu kỳ chuẩn' : 'Tùy chỉnh'}
            </span>
          </div>

          <div className="rounded-xl border border-line bg-surface-subtle/70 p-3.5 transition-all">
            <span className="text-[11px] font-medium text-ink-muted block mb-0.5">
              Cấu trúc kế hoạch
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-lg font-bold text-ink-primary">7</span>
              <span className="text-xs text-ink-muted">giai đoạn</span>
            </div>
            <span className="mt-1 inline-block text-[10px] text-ink-muted">
              Tuần 1 → {totalWeeks}
            </span>
          </div>
        </div>

        {/* Thông tin tóm tắt - Crisp visible frame */}
        <div className="rounded-xl border border-line bg-surface-subtle p-3.5 space-y-2.5 text-xs">
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-ink-body">Niên khóa:</span>
            <span className="font-semibold text-ink-primary">
              {academicYear || 'Chưa thiết lập'} (HK{semester || 1})
            </span>
          </div>
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-ink-body">Tên đợt:</span>
            <span className="font-semibold text-ink-primary truncate max-w-[180px]" title={batchName}>
              {batchName || 'Chưa đặt tên'}
            </span>
          </div>
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-ink-body">Đơn vị quản lý:</span>
            <span className="font-semibold text-ink-primary">Phòng Đào tạo</span>
          </div>
        </div>

        {/* Nút Mở đợt vận hành */}
        {canEdit && status === 'Draft' && onOpenBatch && (
          <div className="pt-1 space-y-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isOpenLoading || isBlockedByOtherOpenBatch}
              onClick={onOpenBatch}
              className="w-full h-10 gap-2 rounded-xl border border-tone-success-border bg-tone-success-bg text-tone-success-strong hover:bg-tone-success-bg hover:border-emerald-400 hover:text-tone-success-strong font-semibold text-xs shadow-2xs transition-all active:scale-[0.98] disabled:opacity-55 disabled:cursor-not-allowed"
            >
              <Play className="size-3.5 fill-current" />
              {isOpenLoading ? 'Đang mở đợt...' : 'Mở đợt vận hành'}
            </Button>
            {isBlockedByOtherOpenBatch && (
              <p
                role="status"
                className="flex items-start gap-2 rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3 py-2 text-[11px] leading-relaxed text-tone-warning-strong"
              >
                <AlertTriangle className="size-3.5 shrink-0 mt-0.5" aria-hidden="true" />
                <span>
                  Hệ thống chỉ cho phép mở 1 đợt KLTN tại một thời điểm. Đợt{' '}
                  <strong className="font-semibold">{activeOpenBatchName}</strong> hiện đang hoạt
                  động — vui lòng đóng đợt đó trước khi mở đợt mới.
                </span>
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
