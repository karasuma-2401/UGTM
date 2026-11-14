import { Calendar, CheckCircle2, Clock3, RotateCcw, Circle } from 'lucide-react';
import { SelectionIndicator } from '@/components/ui/selection-indicator';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import type { MilestoneWithStatus } from '../../hooks/useThesisJourneySummary';
import { formatMilestoneDateShort, getDeadlineInfo } from '../../utils/milestoneFormatters';

interface MilestoneSelectorStripProps {
  items: MilestoneWithStatus[];
  selectedMilestoneId: string | null;
  onSelect: (milestoneId: string) => void;
  isLoading?: boolean;
}

const urgencyTextTone: Record<string, string> = {
  completed: 'text-slate-500',
  overdue: 'text-rose-600 font-medium',
  soon: 'text-amber-700 font-medium',
  normal: 'text-slate-500',
};

export function MilestoneSelectorStrip({
  items,
  selectedMilestoneId,
  onSelect,
  isLoading = false,
}: MilestoneSelectorStripProps) {
  if (isLoading) {
    return (
      <div className="bg-white rounded-2xl border border-slate-200/80 p-4 shadow-xs space-y-3">
        <Skeleton className="h-4 w-36" />
        <Skeleton className="h-1.5 w-full rounded-full" />
        <div className="space-y-2 pt-1">
          <Skeleton className="h-16 w-full rounded-xl" />
          <Skeleton className="h-16 w-full rounded-xl" />
          <Skeleton className="h-16 w-full rounded-xl" />
        </div>
      </div>
    );
  }

  if (items.length === 0) {
    return null;
  }

  const completedCount = items.filter((item) => item.status === 'Accepted').length;
  const progressPercent = Math.round((completedCount / items.length) * 100);

  return (
    <section
      aria-label="Danh sách mốc báo cáo tiến độ"
      className="bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden"
    >
      <div className="px-4 py-3.5 border-b border-slate-100 space-y-2.5">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-sm font-bold text-slate-900">Mốc tiến độ</h2>
          <span className="inline-flex items-center gap-1 text-xs font-semibold text-slate-700 tabular-nums">
            <CheckCircle2 className="size-3.5 text-emerald-600" />
            {completedCount}/{items.length} đạt
          </span>
        </div>

        <div className="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden">
          <div
            className="h-full rounded-full bg-emerald-500 transition-[width] duration-300 ease-out"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Desktop vertical rail / Mobile horizontal strip */}
      <div className="relative isolate flex lg:flex-col overflow-x-auto lg:overflow-x-visible gap-1.5 lg:gap-1 p-2">
        <SelectionIndicator
          activeKey={selectedMilestoneId}
          selector='[data-selection-item][aria-pressed="true"]'
          continuityKey="student-milestone-selector"
          className="rounded-xl bg-slate-100/90 border border-slate-200/90 shadow-2xs"
        />

        {items.map(({ milestone, status, currentVersionNo }, index) => {
          const isSelected = milestone.id === selectedMilestoneId;
          const deadline = getDeadlineInfo(milestone.deadlineUtc, status);
          const StatusIcon =
            status === 'Accepted'
              ? CheckCircle2
              : status === 'NeedRevision'
                ? RotateCcw
                : status === 'Submitted'
                  ? Clock3
                  : Circle;

          const statusDotColor =
            status === 'Accepted'
              ? 'bg-emerald-500'
              : status === 'NeedRevision'
                ? 'bg-amber-500'
                : status === 'Submitted'
                  ? 'bg-[#2054a5]'
                  : 'bg-slate-300';

          return (
            <button
              key={milestone.id}
              type="button"
              data-selection-item
              aria-pressed={isSelected}
              onClick={() => onSelect(milestone.id)}
              className={cn(
                'relative z-10 text-left rounded-xl px-3 py-2.5 transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring cursor-pointer min-w-[230px] lg:min-w-0 flex flex-col gap-1.5',
                isSelected
                  ? 'text-slate-950'
                  : 'text-slate-700 hover:bg-slate-50/90',
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-2 min-w-0">
                  <span
                    className={cn(
                      'inline-flex items-center justify-center size-5 rounded-md text-[11px] font-bold tabular-nums shrink-0 transition-colors',
                      isSelected
                        ? 'bg-slate-900 text-white'
                        : 'bg-slate-100 text-slate-600',
                    )}
                  >
                    {String(index + 1).padStart(2, '0')}
                  </span>
                  <span className={cn('size-1.5 rounded-full shrink-0', statusDotColor)} />
                  <span className="text-xs font-medium text-slate-500 truncate">
                    Mốc {index + 1}
                  </span>
                </div>
                {currentVersionNo > 0 && (
                  <span className="text-[11px] font-medium text-slate-500 tabular-nums shrink-0">
                    v{currentVersionNo}
                  </span>
                )}
              </div>

              <p
                className={cn(
                  'text-sm leading-snug line-clamp-2',
                  isSelected ? 'font-semibold text-slate-950' : 'font-medium text-slate-800',
                )}
                title={milestone.title}
              >
                {milestone.title}
              </p>

              <div className="flex items-center justify-between gap-2 text-[11px]">
                <span className="inline-flex items-center gap-1 text-slate-500 tabular-nums">
                  <Calendar className="size-3 text-slate-400 shrink-0" />
                  {formatMilestoneDateShort(milestone.deadlineUtc)}
                </span>
                <span className={cn('inline-flex items-center gap-1 truncate', urgencyTextTone[deadline.urgency])}>
                  <StatusIcon
                    className={cn(
                      'size-3.5 shrink-0',
                      status === 'Accepted' && 'text-emerald-600',
                    )}
                  />
                  {deadline.label}
                </span>
              </div>
            </button>
          );
        })}
      </div>
    </section>
  );
}
