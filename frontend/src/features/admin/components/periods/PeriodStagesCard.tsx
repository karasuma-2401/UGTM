import React from 'react';
import {
  STANDARD_7_STAGES,
  type StageDto,
  type StandardStageMetadata,
} from '../../types/period.types';

interface PeriodStagesCardProps {
  stages?: StageDto[];
  startDate?: string;
}

function formatDate(dateStr: string): string {
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

function computeStageDate(
  baseStartDate: string,
  offsetDays: number,
): string {
  try {
    const d = new Date(baseStartDate);
    if (isNaN(d.getTime())) return '';
    d.setDate(d.getDate() + offsetDays);
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${dd}/${mm}/${yyyy}`;
  } catch {
    return '';
  }
}

export const PeriodStagesCard: React.FC<PeriodStagesCardProps> = ({
  stages,
  startDate,
}) => {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-base font-bold text-slate-900">7 giai đoạn</h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-slate-100 text-xs text-slate-400">
              <th className="pb-3 font-medium">Giai đoạn</th>
              <th className="pb-3 font-medium">Thời gian</th>
              <th className="pb-3 font-medium">Phụ trách</th>
              <th className="pb-3 text-right font-medium">Thứ tự</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {STANDARD_7_STAGES.map((meta: StandardStageMetadata) => {
              const matchedStage = stages?.find(
                (s) => s.orderIndex === meta.orderIndex || s.stageCode === meta.stageCode,
              );

              let dateRangeText = '';
              if (matchedStage?.startDate && matchedStage?.endDate) {
                dateRangeText = `${formatDate(matchedStage.startDate)} – ${formatDate(matchedStage.endDate)}`;
              } else if (startDate) {
                const s = computeStageDate(startDate, meta.startOffsetDays);
                const e = computeStageDate(startDate, meta.endOffsetDays);
                if (s && e) {
                  dateRangeText = `${s} – ${e}`;
                }
              }

              return (
                <tr key={meta.orderIndex} className="hover:bg-slate-50/50 transition-colors">
                  <td className="py-3.5 font-medium text-slate-800">
                    <div>{meta.shortName}</div>
                    <div className="text-[11px] text-slate-400 font-normal">
                      {meta.stageName}
                    </div>
                  </td>
                  <td className="py-3.5 text-slate-600">
                    <span className="font-medium text-slate-700">{meta.durationLabel}</span>
                    {dateRangeText && (
                      <span className="ml-2 text-xs text-slate-400">
                        ({dateRangeText})
                      </span>
                    )}
                  </td>
                  <td className="py-3.5 text-slate-600">
                    {meta.actor}
                  </td>
                  <td className="py-3.5 text-right font-bold text-[#2054A5]">
                    {String(meta.orderIndex).padStart(2, '0')}
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