import React, { useState } from 'react';
import { Calendar, Flag, CheckCircle2, Clock } from 'lucide-react';
import type { ComputedStageItem } from './PeriodStagesCard';

interface PeriodTimelineProps {
  computedStages: ComputedStageItem[];
  totalWeeks: number;
  syllabusDeadline?: string;
  midtermDeadline?: string;
  finalEvaluationDeadline?: string;
  defenseDeadline?: string;
}

function formatDateDisplay(dateStr?: string): string {
  if (!dateStr) return '';
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

export const PeriodTimeline: React.FC<PeriodTimelineProps> = ({
  computedStages,
  totalWeeks,
  syllabusDeadline,
  midtermDeadline,
  finalEvaluationDeadline,
  defenseDeadline,
}) => {
  const [hoveredStageIndex, setHoveredStageIndex] = useState<number | null>(null);

  const milestones = [
    {
      name: 'Hạn Đề cương',
      stageIndex: 1,
      endWeek: computedStages[1]?.endWeek ?? 2,
      date: syllabusDeadline || computedStages[1]?.endDate,
    },
    {
      name: 'Hạn Giữa kỳ',
      stageIndex: 3,
      endWeek: computedStages[3]?.endWeek ?? 10,
      date: midtermDeadline || computedStages[3]?.endDate,
    },
    {
      name: 'Hạn Báo cáo',
      stageIndex: 4,
      endWeek: computedStages[4]?.endWeek ?? 18,
      date: finalEvaluationDeadline || computedStages[4]?.endDate,
    },
    {
      name: 'Hạn Bảo vệ',
      stageIndex: 5,
      endWeek: computedStages[5]?.endWeek ?? 20,
      date: defenseDeadline || computedStages[5]?.endDate,
    },
  ];

  const activeStage = hoveredStageIndex !== null ? computedStages[hoveredStageIndex] : null;

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs transition-all">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-slate-500" />
          <span className="text-sm font-bold text-slate-900">
            Dòng thời gian kế hoạch ({totalWeeks} tuần)
          </span>
        </div>

        <span className="text-xs text-slate-400">
          Rê chuột để xem chi tiết từng giai đoạn
        </span>
      </div>

      {/* Timeline Segments Bar - No text truncation with weighted flex scaling */}
      <div
        className="relative mb-2 overflow-x-auto pb-1"
        onMouseLeave={() => setHoveredStageIndex(null)}
      >
        <div className="flex h-11 min-w-[680px] w-full rounded-xl border border-slate-200 bg-slate-100/70 p-1 gap-1">
          {computedStages.map((stage, idx) => {
            const isHovered = hoveredStageIndex === idx;
            // Weighted flex: ensure 1-week stages have at least flex-2.2 and min-width 78px so their name is NEVER truncated with "..."
            const flexWeight = Math.max(2.2, stage.weeks);

            return (
              <div
                key={stage.orderIndex}
                onMouseEnter={() => setHoveredStageIndex(idx)}
                style={{ flex: `${flexWeight} 1 0%`, minWidth: '78px' }}
                className={`group relative flex h-full cursor-pointer items-center justify-center rounded-lg border transition-all duration-200 px-2 select-none ${
                  isHovered
                    ? 'border-primary/50 bg-blue-50/90 text-primary shadow-xs z-10 scale-[1.01]'
                    : 'border-slate-200/80 bg-white/95 text-slate-700 hover:border-slate-300 hover:bg-slate-50'
                }`}
              >
                <div className="flex items-center justify-center text-center">
                  <span
                    className={`whitespace-nowrap text-xs font-semibold tracking-tight ${
                      isHovered ? 'text-primary' : 'text-slate-800'
                    }`}
                    title={`${stage.stageName} (${stage.weeks} tuần)`}
                  >
                    {stage.shortName}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Milestone Flags row - Clean Neutral Style */}
      <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
        {milestones.map((ms) => {
          return (
            <div
              key={ms.name}
              className="flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs shadow-2xs transition-all hover:bg-white hover:border-slate-300"
            >
              <div className="flex items-center gap-2 min-w-0">
                <div className="flex size-5 shrink-0 items-center justify-center rounded-md bg-slate-200/70 text-slate-700">
                  <Flag className="size-2.5" />
                </div>
                <div className="truncate">
                  <span className="font-semibold text-slate-800 block truncate">
                    {ms.name}
                  </span>
                  <span className="text-[10px] text-slate-400">
                    Tuần {ms.endWeek}
                  </span>
                </div>
              </div>
              <span className="font-bold text-slate-800 shrink-0 ml-1.5 text-xs">
                {formatDateDisplay(ms.date)}
              </span>
            </div>
          );
        })}
      </div>

      {/* Detail Info Bar - Only slides down when hovering a stage */}
      {activeStage && (
        <div className="mt-3 overflow-hidden rounded-xl border border-blue-200/90 bg-blue-50/60 shadow-2xs transition-all duration-300">
          <div
            aria-hidden="true"
            className="h-0.5 w-full bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]"
          />
          <div
            key={`stage-${activeStage.orderIndex}`}
            className="flex min-h-[42px] w-full flex-wrap items-center justify-between gap-3 px-4 py-2 text-xs text-slate-700 animate-in fade-in slide-in-from-top-3 duration-300 ease-out"
          >
            <div className="flex items-center gap-2">
              <span className="inline-flex size-5 items-center justify-center rounded-md bg-primary text-white font-bold text-[10px]">
                0{activeStage.orderIndex}
              </span>
              <span className="font-bold text-slate-900">
                {activeStage.stageName}
              </span>
              <span className="text-slate-300">•</span>
              <span className="font-semibold text-primary">
                {activeStage.weekLabel} ({activeStage.weeks} tuần)
              </span>
            </div>

            <div className="flex items-center gap-4 text-slate-600">
              <div className="flex items-center gap-1.5">
                <Calendar className="size-3.5 text-slate-400" />
                <span>
                  {formatDateDisplay(activeStage.startDate)} –{' '}
                  {formatDateDisplay(activeStage.endDate)}
                </span>
              </div>
              <div className="flex items-center gap-1.5">
                <CheckCircle2 className="size-3.5 text-emerald-600" />
                <span className="font-medium text-slate-700">
                  Phụ trách: {activeStage.actor}
                </span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
