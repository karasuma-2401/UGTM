import React from 'react';
import { SlidersHorizontal } from 'lucide-react';
import { DatePicker } from '@/components/ui/calendar';
import { Button } from '@/components/ui/button';

interface PeriodDeadlinesCardProps {
  syllabusDeadline: string;
  midtermDeadline: string;
  finalEvaluationDeadline: string;
  defenseDeadline: string;
  syllabusMinDate?: string;
  midtermMinDate?: string;
  finalEvaluationMinDate?: string;
  defenseMinDate?: string;
  canEdit: boolean;
  onSyllabusDeadlineChange: (val: string) => void;
  onMidtermDeadlineChange: (val: string) => void;
  onFinalEvaluationDeadlineChange: (val: string) => void;
  onDefenseDeadlineChange: (val: string) => void;
  onOpenGateDetails?: () => void;
}

export const PeriodDeadlinesCard: React.FC<PeriodDeadlinesCardProps> = ({
  syllabusDeadline,
  midtermDeadline,
  finalEvaluationDeadline,
  defenseDeadline,
  syllabusMinDate,
  midtermMinDate,
  finalEvaluationMinDate,
  defenseMinDate,
  canEdit,
  onSyllabusDeadlineChange,
  onMidtermDeadlineChange,
  onFinalEvaluationDeadlineChange,
  onDefenseDeadlineChange,
  onOpenGateDetails,
}) => {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs transition-all hover:border-slate-300">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-bold text-slate-900 tracking-tight">
            Mốc thời gian Deadline
          </h2>
          <p className="text-xs text-slate-500">
            Hạn chót các cổng nộp bài và thẩm định kết quả
          </p>
        </div>

        {onOpenGateDetails && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onOpenGateDetails}
            className="h-8 gap-2 rounded-lg border-slate-200 bg-white text-xs font-semibold text-slate-700 shadow-2xs hover:bg-slate-50 hover:text-slate-900 transition-all active:scale-[0.98]"
          >
            <SlidersHorizontal className="size-3.5 text-slate-500" />
            Chi tiết cổng thẩm định
          </Button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {/* Hạn Đề cương */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 transition-all hover:border-slate-300 hover:bg-slate-50/90">
          <div className="mb-2 flex items-center justify-between">
            <label className="text-xs font-bold text-slate-800">Hạn Đề cương</label>
            <span className="rounded-md border border-slate-200 bg-white px-2 py-0.5 text-[10px] font-semibold text-slate-600 shadow-2xs">
              Giai đoạn 2
            </span>
          </div>
          <DatePicker
            aria-label="Hạn đề cương"
            disabled={!canEdit}
            value={syllabusDeadline}
            min={syllabusMinDate}
            onValueChange={(value) => onSyllabusDeadlineChange(value)}
            className="h-9 text-sm rounded-lg border-slate-200 bg-white hover:border-slate-300 transition-colors"
          />
        </div>

        {/* Hạn Giữa kỳ */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 transition-all hover:border-slate-300 hover:bg-slate-50/90">
          <div className="mb-2 flex items-center justify-between">
            <label className="text-xs font-bold text-slate-800">Hạn Giữa kỳ</label>
            <span className="rounded-md border border-slate-200 bg-white px-2 py-0.5 text-[10px] font-semibold text-slate-600 shadow-2xs">
              Giai đoạn 4
            </span>
          </div>
          <DatePicker
            aria-label="Hạn giữa kỳ"
            disabled={!canEdit}
            value={midtermDeadline}
            min={midtermMinDate}
            onValueChange={(value) => onMidtermDeadlineChange(value)}
            className="h-9 text-sm rounded-lg border-slate-200 bg-white hover:border-slate-300 transition-colors"
          />
        </div>

        {/* Hạn Cuối kỳ */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 transition-all hover:border-slate-300 hover:bg-slate-50/90">
          <div className="mb-2 flex items-center justify-between">
            <label className="text-xs font-bold text-slate-800">Hạn Báo cáo cuối kỳ</label>
            <span className="rounded-md border border-slate-200 bg-white px-2 py-0.5 text-[10px] font-semibold text-slate-600 shadow-2xs">
              Giai đoạn 5
            </span>
          </div>
          <DatePicker
            aria-label="Hạn cuối kỳ"
            disabled={!canEdit}
            value={finalEvaluationDeadline}
            min={finalEvaluationMinDate}
            onValueChange={(value) => onFinalEvaluationDeadlineChange(value)}
            className="h-9 text-sm rounded-lg border-slate-200 bg-white hover:border-slate-300 transition-colors"
          />
        </div>

        {/* Hạn Bảo vệ / Lưu chiểu */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3.5 transition-all hover:border-slate-300 hover:bg-slate-50/90">
          <div className="mb-2 flex items-center justify-between">
            <label className="text-xs font-bold text-slate-800">Hạn Bảo vệ / Lưu chiểu</label>
            <span className="rounded-md border border-slate-200 bg-white px-2 py-0.5 text-[10px] font-semibold text-slate-600 shadow-2xs">
              Giai đoạn 6
            </span>
          </div>
          <DatePicker
            aria-label="Hạn bảo vệ / lưu chiểu"
            disabled={!canEdit}
            value={defenseDeadline}
            min={defenseMinDate}
            onValueChange={(value) => onDefenseDeadlineChange(value)}
            className="h-9 text-sm rounded-lg border-slate-200 bg-white hover:border-slate-300 transition-colors"
          />
        </div>
      </div>
    </div>
  );
};
