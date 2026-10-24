import React from 'react';
import { Play } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PeriodSummaryCardProps {
  academicYear: string;
  semester: number;
  batchName: string;
  status?: string;
  canEdit: boolean;
  onOpenBatch?: () => void;
  isOpenLoading?: boolean;
}

const STATUS_MAP: Record<string, { label: string; className: string }> = {
  Draft: {
    label: 'Bản nháp',
    className: 'bg-amber-50 text-amber-700 ring-1 ring-amber-500/20',
  },
  Open: {
    label: 'Đang hoạt động',
    className: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-500/20',
  },
  Closed: {
    label: 'Đã đóng',
    className: 'bg-slate-100 text-slate-700 ring-1 ring-slate-400/20',
  },
  Archived: {
    label: 'Đã lưu trữ',
    className: 'bg-indigo-50 text-indigo-700 ring-1 ring-indigo-500/20',
  },
};

export const PeriodSummaryCard: React.FC<PeriodSummaryCardProps> = ({
  academicYear,
  semester,
  batchName,
  status = 'Draft',
  canEdit,
  onOpenBatch,
  isOpenLoading = false,
}) => {
  const currentStatus = STATUS_MAP[status] || STATUS_MAP.Draft;

  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-base font-bold text-slate-900">
          Kế hoạch đang chỉnh
        </h2>
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${currentStatus.className}`}
        >
          {currentStatus.label}
        </span>
      </div>

      <div className="space-y-3.5 text-sm">
        <div>
          <div className="text-xs text-slate-400 font-medium">Năm học</div>
          <div className="font-semibold text-slate-800">
            {academicYear || 'Chưa thiết lập'}
          </div>
        </div>

        <div>
          <div className="text-xs text-slate-400 font-medium">Học kỳ</div>
          <div className="font-semibold text-slate-800">
            HK{semester || 1}
          </div>
        </div>

        <div>
          <div className="text-xs text-slate-400 font-medium">Đợt</div>
          <div className="font-semibold text-slate-800">
            {batchName || 'Chưa đặt tên'}
          </div>
        </div>

        <div>
          <div className="text-xs text-slate-400 font-medium">Thời lượng</div>
          <div className="font-semibold text-slate-800">
            21 tuần - 7 giai đoạn
          </div>
        </div>

        <div>
          <div className="text-xs text-slate-400 font-medium">Đơn vị quản lý</div>
          <div className="font-semibold text-slate-800">
            Phòng Đào tạo
          </div>
        </div>

        <div className="pt-2 border-t border-slate-100">
          <div className="mb-2 text-xs font-semibold text-slate-700">
            Lưu ý xử lý
          </div>
          <ul className="space-y-1.5 text-xs text-slate-500">
            <li className="flex items-start gap-1.5">
              <span className="text-slate-400">•</span>
              <span>Giai đoạn đúng thứ tự</span>
            </li>
            <li className="flex items-start gap-1.5">
              <span className="text-slate-400">•</span>
              <span>Deadline theo lịch được duyệt</span>
            </li>
            <li className="flex items-start gap-1.5">
              <span className="text-slate-400">•</span>
              <span>Khoa tra cứu kế hoạch đã công bố</span>
            </li>
          </ul>
        </div>

        {canEdit && status === 'Draft' && onOpenBatch && (
          <div className="pt-3 border-t border-slate-100">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isOpenLoading}
              onClick={onOpenBatch}
              className="w-full gap-2 text-emerald-700 border-emerald-200 hover:bg-emerald-50 hover:text-emerald-800 font-medium text-xs h-9"
            >
              <Play className="size-3.5" />
              {isOpenLoading ? 'Đang mở đợt...' : 'Mở đợt vận hành'}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};