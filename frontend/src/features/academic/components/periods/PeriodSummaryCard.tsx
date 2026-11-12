import React from 'react';
import { Play } from 'lucide-react';
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
}

const STATUS_MAP: Record<
  string,
  { label: string; bg: string; text: string; dot: string; ping: string }
> = {
  Draft: {
    label: 'Bản nháp',
    bg: 'bg-amber-50 border-amber-300',
    text: 'text-amber-900',
    dot: 'bg-amber-500',
    ping: 'bg-amber-400',
  },
  Open: {
    label: 'Đang hoạt động',
    bg: 'bg-emerald-50 border-emerald-300',
    text: 'text-emerald-900',
    dot: 'bg-emerald-500',
    ping: 'bg-emerald-400',
  },
  Closed: {
    label: 'Đã đóng',
    bg: 'bg-slate-100 border-slate-300',
    text: 'text-slate-800',
    dot: 'bg-slate-500',
    ping: 'bg-slate-400',
  },
  Archived: {
    label: 'Đã lưu trữ',
    bg: 'bg-slate-100 border-slate-300',
    text: 'text-slate-800',
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
}) => {
  const currentStatus = STATUS_MAP[status] || STATUS_MAP.Draft;
  const isStandard21Weeks = totalWeeks === 21;

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs transition-all hover:border-slate-300">
      <div className="mb-5 flex items-center justify-between gap-3">
        <h2 className="text-base font-bold text-slate-900 tracking-tight">
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
          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3.5 transition-all">
            <span className="text-[11px] font-medium text-slate-500 block mb-0.5">
              Thời lượng chu kỳ
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-lg font-bold text-slate-900">
                {totalWeeks}
              </span>
              <span className="text-xs text-slate-500">tuần</span>
            </div>
            <span className="mt-1 inline-block text-[10px] text-slate-500">
              {isStandard21Weeks ? 'Chu kỳ chuẩn' : 'Tùy chỉnh'}
            </span>
          </div>

          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3.5 transition-all">
            <span className="text-[11px] font-medium text-slate-500 block mb-0.5">
              Cấu trúc kế hoạch
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-lg font-bold text-slate-900">7</span>
              <span className="text-xs text-slate-500">giai đoạn</span>
            </div>
            <span className="mt-1 inline-block text-[10px] text-slate-500">
              Tuần 1 → {totalWeeks}
            </span>
          </div>
        </div>

        {/* Thông tin tóm tắt - Crisp visible frame */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 space-y-2.5 text-xs">
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-slate-600">Niên khóa:</span>
            <span className="font-semibold text-slate-900">
              {academicYear || 'Chưa thiết lập'} (HK{semester || 1})
            </span>
          </div>
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-slate-600">Tên đợt:</span>
            <span className="font-semibold text-slate-900 truncate max-w-[180px]" title={batchName}>
              {batchName || 'Chưa đặt tên'}
            </span>
          </div>
          <div className="flex items-center justify-between gap-2">
            <span className="font-medium text-slate-600">Đơn vị quản lý:</span>
            <span className="font-semibold text-slate-900">Phòng Đào tạo</span>
          </div>
        </div>

        {/* Nút Mở đợt vận hành */}
        {canEdit && status === 'Draft' && onOpenBatch && (
          <div className="pt-1">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isOpenLoading}
              onClick={onOpenBatch}
              className="w-full h-10 gap-2 rounded-xl border border-emerald-300 bg-emerald-50/90 text-emerald-800 hover:bg-emerald-100 hover:border-emerald-400 hover:text-emerald-900 font-semibold text-xs shadow-2xs transition-all active:scale-[0.98]"
            >
              <Play className="size-3.5 fill-current" />
              {isOpenLoading ? 'Đang mở đợt...' : 'Mở đợt vận hành'}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};
