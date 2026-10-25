import React from 'react';
import { RotateCcw, Minus, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface ComputedStageItem {
  orderIndex: number;
  stageCode: string;
  stageName: string;
  shortName: string;
  actor: string;
  weeks: number;
  startWeek: number;
  endWeek: number;
  weekLabel: string;
  startDate: string;
  endDate: string;
}

interface PeriodStagesCardProps {
  computedStages: ComputedStageItem[];
  canEdit: boolean;
  onWeeksChange: (index: number, newWeeks: number) => void;
  onResetDefaultWeeks: () => void;
  totalWeeks: number;
}

function formatDateDisplay(dateStr: string): string {
  try {
    const [year, month, day] = dateStr.split('-');
    if (year && month && day) {
      return `${day}/${month}/${year}`;
    }
    return dateStr;
  } catch {
    return dateStr;
  }
}

export const PeriodStagesCard: React.FC<PeriodStagesCardProps> = ({
  computedStages,
  canEdit,
  onWeeksChange,
  onResetDefaultWeeks,
  totalWeeks,
}) => {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h2 className="text-base font-bold text-slate-900">7 giai đoạn</h2>
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
              totalWeeks === 21
                ? 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-500/20'
                : 'bg-blue-50 text-[#2054A5] ring-1 ring-blue-500/20'
            }`}
          >
            Tổng: {totalWeeks} tuần {totalWeeks === 21 ? '(Chu kỳ chuẩn)' : ''}
          </span>
        </div>

        {canEdit && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onResetDefaultWeeks}
            className="h-8 gap-1.5 text-xs font-medium text-slate-600 hover:text-slate-900"
          >
            <RotateCcw className="size-3.5" />
            Đặt lại 21 tuần mẫu
          </Button>
        )}
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-slate-100 text-xs text-slate-400">
              <th className="pb-3 font-medium">Giai đoạn</th>
              <th className="pb-3 font-medium">Số tuần</th>
              <th className="pb-3 font-medium">Thời gian (Tự động tính)</th>
              <th className="pb-3 font-medium">Phụ trách</th>
              <th className="pb-3 text-right font-medium">Thứ tự</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {computedStages.map((stage, index) => {
              return (
                <tr key={stage.orderIndex} className="hover:bg-slate-50/50 transition-colors">
                  <td className="py-3.5 font-medium text-slate-800">
                    <div>{stage.shortName}</div>
                    <div className="text-[11px] text-slate-400 font-normal">
                      {stage.stageName}
                    </div>
                  </td>

                  <td className="py-3.5">
                    {canEdit ? (
                      <div className="flex items-center gap-1.5">
                        <button
                          type="button"
                          onClick={() => onWeeksChange(index, Math.max(1, stage.weeks - 1))}
                          disabled={stage.weeks <= 1}
                          className="flex size-7 items-center justify-center rounded-md border border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed"
                          title="Giảm 1 tuần"
                        >
                          <Minus className="size-3" />
                        </button>
                        <input
                          type="number"
                          min={1}
                          max={52}
                          value={stage.weeks}
                          onChange={(e) => {
                            const val = parseInt(e.target.value, 10);
                            onWeeksChange(index, isNaN(val) || val < 1 ? 1 : val);
                          }}
                          className="h-7 w-12 rounded-md border border-slate-200 bg-white text-center text-xs font-bold text-slate-800 shadow-2xs focus:border-[#2054A5] focus:outline-hidden focus:ring-1 focus:ring-[#2054A5]"
                        />
                        <button
                          type="button"
                          onClick={() => onWeeksChange(index, stage.weeks + 1)}
                          className="flex size-7 items-center justify-center rounded-md border border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100"
                          title="Tăng 1 tuần"
                        >
                          <Plus className="size-3" />
                        </button>
                        <span className="text-xs text-slate-500 font-medium ml-1">tuần</span>
                      </div>
                    ) : (
                      <span className="font-semibold text-slate-800 text-xs">
                        {stage.weeks} tuần
                      </span>
                    )}
                  </td>

                  <td className="py-3.5 text-slate-600">
                    <span className="font-medium text-slate-800">{stage.weekLabel}</span>
                    <span className="ml-2 text-xs text-slate-400">
                      ({formatDateDisplay(stage.startDate)} – {formatDateDisplay(stage.endDate)})
                    </span>
                  </td>

                  <td className="py-3.5 text-slate-600">
                    {stage.actor}
                  </td>

                  <td className="py-3.5 text-right font-bold text-[#2054A5]">
                    {String(stage.orderIndex).padStart(2, '0')}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};
