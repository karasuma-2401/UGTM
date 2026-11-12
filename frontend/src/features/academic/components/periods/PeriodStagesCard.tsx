import React from 'react';
import { RotateCcw, Minus, Plus, Calendar, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PeriodTimeline } from './PeriodTimeline';

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
  syllabusDeadline?: string;
  midtermDeadline?: string;
  finalEvaluationDeadline?: string;
  defenseDeadline?: string;
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
  syllabusDeadline,
  midtermDeadline,
  finalEvaluationDeadline,
  defenseDeadline,
}) => {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs transition-all hover:border-slate-300 space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2.5">
            <h2 className="text-base font-bold text-slate-900 tracking-tight">
              Tiến trình 7 giai đoạn khóa luận
            </h2>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700">
              Tổng {totalWeeks} tuần {totalWeeks === 21 ? '(Chu kỳ chuẩn)' : ''}
            </span>
          </div>
          <p className="text-xs text-slate-500">
            Điều chỉnh số tuần cho từng giai đoạn, hệ thống sẽ tự động cập nhật dải thời gian
          </p>
        </div>

        {canEdit && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onResetDefaultWeeks}
            className="h-8 gap-1.5 rounded-lg border-slate-200 bg-white text-xs font-semibold text-slate-600 shadow-2xs hover:bg-slate-50 hover:text-slate-900 transition-all active:scale-[0.98]"
          >
            <RotateCcw className="size-3.5" />
            Đặt lại 21 tuần mẫu
          </Button>
        )}
      </div>

      {/* Visual Timeline Bar */}
      <PeriodTimeline
        computedStages={computedStages}
        totalWeeks={totalWeeks}
        syllabusDeadline={syllabusDeadline}
        midtermDeadline={midtermDeadline}
        finalEvaluationDeadline={finalEvaluationDeadline}
        defenseDeadline={defenseDeadline}
      />

      {/* Table of Stages */}
      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50/70 text-xs text-slate-500">
              <th className="py-3 px-4 font-semibold text-center w-16">Thứ tự</th>
              <th className="py-3 px-4 font-semibold">Giai đoạn & Quy trình</th>
              <th className="py-3 px-4 font-semibold w-44 text-center">Số tuần</th>
              <th className="py-3 px-4 font-semibold">Thời gian áp dụng</th>
              <th className="py-3 px-4 font-semibold">Phụ trách</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {computedStages.map((stage, index) => {
              return (
                <tr
                  key={stage.orderIndex}
                  className="hover:bg-slate-50/70 transition-colors group/row"
                >
                  {/* Cột Thứ tự bên trái */}
                  <td className="py-3.5 px-4 text-center">
                    <span className="inline-flex size-6 items-center justify-center rounded-md bg-slate-100 font-semibold text-xs text-slate-700">
                      0{stage.orderIndex}
                    </span>
                  </td>

                  {/* Tên giai đoạn */}
                  <td className="py-3.5 px-4">
                    <div className="font-semibold text-slate-900 flex items-center gap-2">
                      <span>{stage.shortName}</span>
                      <span className="text-[11px] font-normal text-slate-400">
                        ({stage.weekLabel})
                      </span>
                    </div>
                    <div className="text-xs text-slate-500 font-normal">
                      {stage.stageName}
                    </div>
                  </td>

                  {/* Số tuần */}
                  <td className="py-3.5 px-4">
                    {canEdit ? (
                      <div className="flex items-center justify-center gap-1.5">
                        <button
                          type="button"
                          onClick={() => onWeeksChange(index, Math.max(1, stage.weeks - 1))}
                          disabled={stage.weeks <= 1}
                          className="flex size-7 items-center justify-center rounded-md border border-slate-200 bg-white text-slate-600 shadow-2xs hover:bg-slate-100 hover:border-slate-300 active:scale-95 disabled:opacity-30 disabled:cursor-not-allowed transition-all"
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
                          className="h-7 w-12 rounded-md border border-slate-200 bg-white text-center text-xs font-bold text-slate-900 shadow-2xs focus:border-primary focus:outline-hidden focus:ring-1 focus:ring-primary/20 transition-all"
                        />
                        <button
                          type="button"
                          onClick={() => onWeeksChange(index, stage.weeks + 1)}
                          className="flex size-7 items-center justify-center rounded-md border border-slate-200 bg-white text-slate-600 shadow-2xs hover:bg-slate-100 hover:border-slate-300 active:scale-95 transition-all"
                          title="Tăng 1 tuần"
                        >
                          <Plus className="size-3" />
                        </button>
                        <span className="text-xs text-slate-500 font-medium ml-1">
                          tuần
                        </span>
                      </div>
                    ) : (
                      <div className="text-center font-bold text-slate-800 text-xs">
                        {stage.weeks} tuần
                      </div>
                    )}
                  </td>

                  {/* Thời gian */}
                  <td className="py-3.5 px-4 text-slate-700">
                    <div className="flex items-center gap-1.5 font-medium text-xs">
                      <Calendar className="size-3.5 text-slate-400" />
                      <span>
                        {formatDateDisplay(stage.startDate)} – {formatDateDisplay(stage.endDate)}
                      </span>
                    </div>
                  </td>

                  {/* Phụ trách - Clean neutral badge */}
                  <td className="py-3.5 px-4">
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                      <UserCheck className="size-3 text-slate-400" />
                      <span>{stage.actor}</span>
                    </span>
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
