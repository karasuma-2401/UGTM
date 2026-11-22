import { useMemo, useState } from 'react';
import { X } from 'lucide-react';
import type { UserActivitySummary } from '@/features/auth/types/auth.types';
import { cn } from '@/lib/utils';
import { buildContributionWeeks, type HeatmapCell } from '../utils/profileFormatters';

interface ActivityContributionGridProps {
  summary: UserActivitySummary;
  selectedDate: string | null;
  onSelectDate: (date: string | null) => void;
}

const DAY_LABELS = ['T2', '', 'T4', '', 'T6', '', 'CN'] as const;

function cellLevelClasses(cell: HeatmapCell, isSelected: boolean): string {
  if (!cell.inRange) {
    return 'opacity-0 pointer-events-none';
  }
  const base =
    cell.level === 0
      ? 'bg-surface-muted/85 border-line/80 hover:border-line-strong'
      : cell.level === 1
        ? 'bg-blue-200/80 border-blue-300/80 dark:bg-blue-950/90 dark:border-blue-800'
        : cell.level === 2
          ? 'bg-blue-400/85 border-blue-500/70 dark:bg-blue-700/90 dark:border-blue-600'
          : 'bg-primary border-primary dark:bg-blue-500 dark:border-blue-400';

  const alertRing =
    cell.alertCount > 0 ? 'ring-1 ring-amber-500 dark:ring-amber-400' : '';
  const selectedRing = isSelected
    ? 'ring-2 ring-offset-1 ring-offset-surface-card ring-primary relative z-10'
    : '';

  return cn(base, alertRing, selectedRing);
}

export function ActivityContributionGrid({
  summary,
  selectedDate,
  onSelectDate,
}: ActivityContributionGridProps) {
  const [hoveredCell, setHoveredCell] = useState<HeatmapCell | null>(null);

  const weeks = useMemo(() => buildContributionWeeks(summary.days, 36), [summary.days]);
  const activePreview =
    hoveredCell ??
    (selectedDate
      ? weeks.flatMap((w) => w.cells).find((c) => c.date === selectedDate) ?? null
      : null);

  return (
    <div className="rounded-xl border border-line/80 bg-surface-subtle/40 p-4 space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-xs text-ink-body">
          <span className="font-semibold text-ink-primary tabular-nums">
            {summary.totalEvents}
          </span>{' '}
          sự kiện trong 36 tuần qua trên{' '}
          <span className="font-semibold text-ink-primary tabular-nums">
            {summary.activeDaysCount}
          </span>{' '}
          ngày hoạt động
        </div>

        <div className="flex items-center gap-2 text-xs">
          {activePreview ? (
            <span className="font-medium tabular-nums text-ink-primary">
              {activePreview.label}: {activePreview.totalCount} sự kiện
              {activePreview.alertCount > 0 ? (
                <span className="text-tone-warning-text">
                  {' '}
                  ({activePreview.alertCount} cảnh báo)
                </span>
              ) : null}
            </span>
          ) : (
            <span className="text-ink-muted">Bấm vào ô ngày để lọc sự kiện bên dưới</span>
          )}
          {selectedDate && (
            <button
              type="button"
              onClick={() => onSelectDate(null)}
              className="inline-flex items-center gap-1 rounded-md border border-line-input bg-surface-card px-2 py-0.5 text-[11px] font-medium text-ink-secondary hover:bg-surface-hover cursor-pointer"
            >
              <X className="size-3" aria-hidden="true" />
              Bỏ lọc ngày
            </button>
          )}
        </div>
      </div>

      {/* Full-width fluid heatmap with horizontal scroll fallback only on narrow phones */}
      <div className="overflow-x-auto p-1 -m-1">
        <div className="flex w-full min-w-[560px] items-stretch gap-2">
          {/* Weekday labels aligned to the 7 cell rows */}
          <div className="flex shrink-0 flex-col gap-1 pr-1 select-none">
            <div className="h-4" aria-hidden="true" />
            <div className="grid flex-1 grid-rows-7 gap-1 text-[10px] font-medium text-ink-muted">
              {DAY_LABELS.map((label, idx) => (
                <span key={idx} className="flex items-center leading-none">
                  {label}
                </span>
              ))}
            </div>
          </div>

          {/* 36-week fluid matrix */}
          <div
            className="grid flex-1 gap-1"
            style={{
              gridTemplateColumns: `repeat(${weeks.length}, minmax(0, 1fr))`,
            }}
          >
            {weeks.map((week) => (
              <div key={week.weekStart} className="flex min-w-0 flex-col gap-1">
                <div className="relative h-4">
                  {week.monthLabel && (
                    <span className="absolute left-0 top-0 text-[10px] font-medium text-ink-muted whitespace-nowrap select-none">
                      {week.monthLabel}
                    </span>
                  )}
                </div>
                <div className="grid grid-rows-7 gap-1">
                  {week.cells.map((cell) => {
                    const isSelected = selectedDate === cell.date;
                    return (
                      <button
                        key={cell.date}
                        type="button"
                        disabled={!cell.inRange}
                        aria-pressed={isSelected}
                        aria-label={`${cell.label}: ${cell.totalCount} sự kiện${cell.alertCount > 0 ? `, ${cell.alertCount} cảnh báo` : ''}`}
                        onMouseEnter={() => setHoveredCell(cell)}
                        onMouseLeave={() => setHoveredCell(null)}
                        onFocus={() => setHoveredCell(cell)}
                        onBlur={() => setHoveredCell(null)}
                        onClick={() => onSelectDate(isSelected ? null : cell.date)}
                        className={cn(
                          'w-full aspect-square rounded-[3px] border transition-colors duration-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary cursor-pointer',
                          cellLevelClasses(cell, isSelected),
                        )}
                      />
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Legend */}
      <div className="flex flex-wrap items-center justify-between gap-2 pt-1 text-[11px] text-ink-muted">
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded-[2px] bg-surface-muted ring-1 ring-amber-500" />
          Có đăng nhập lỗi / bị từ chối
        </span>

        <div className="flex items-center gap-1.5 select-none">
          <span>Ít</span>
          <span className="size-3 rounded-[3px] border border-line/80 bg-surface-muted/85" />
          <span className="size-3 rounded-[3px] border border-blue-300/80 bg-blue-200/80 dark:border-blue-800 dark:bg-blue-950/90" />
          <span className="size-3 rounded-[3px] border border-blue-500/70 bg-blue-400/85 dark:border-blue-600 dark:bg-blue-700/90" />
          <span className="size-3 rounded-[3px] border border-primary bg-primary dark:border-blue-400 dark:bg-blue-500" />
          <span>Nhiều</span>
        </div>
      </div>
    </div>
  );
}
