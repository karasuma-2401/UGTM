import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { SelectionIndicator } from './selection-indicator';
import { SELECTION_TONES, type SelectionTone } from './selection-tones';
import { Skeleton } from './skeleton';

export interface StatisticItem {
  key: string;
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  tone?: SelectionTone;
  onClick?: () => void;
  active?: boolean;
  actionLabel?: string;
}

export interface StatisticsBarProps {
  items: StatisticItem[];
  className?: string;
  loading?: boolean;
  compact?: boolean;
  layout?: 'row' | 'card';
}

export function StatisticsBar({
  items,
  className,
  loading = false,
  compact = false,
  layout = 'row',
}: StatisticsBarProps) {
  const isCard = layout === 'card';
  const activeItem = !loading ? items.find((item) => Boolean(item.active)) : undefined;
  const activeColors = activeItem
    ? SELECTION_TONES[activeItem.tone ?? 'muted']
    : SELECTION_TONES.primary;

  return (
    <dl
      aria-busy={loading}
      className={cn(
        'relative isolate',
        isCard
          ? 'grid gap-2 rounded-2xl border border-slate-200/80 bg-slate-50/50 p-2'
          : 'grid auto-cols-fr grid-flow-col gap-1.5 rounded-xl border border-slate-200/80 bg-slate-100/60 p-1.5',
        className,
        compact && 'flex flex-wrap border-0 bg-transparent p-0 gap-2',
      )}
    >
      <SelectionIndicator
        activeKey={activeItem?.key ?? null}
        selector='[data-selection-item][data-active="true"]'
        className={cn(
          'z-0 border-[1.5px] border-transparent shadow-xs',
          isCard ? 'rounded-xl' : 'rounded-lg',
          activeColors.activeIndicator,
        )}
      />
      {items.map(({ key, label, value, icon, tone = 'muted', onClick, active, actionLabel }) => {
        const colors = SELECTION_TONES[tone];
        const isActive = !loading && Boolean(active);
        return (
          <div
            key={key}
            data-selection-item
            data-active={isActive ? 'true' : undefined}
            className={cn(
              'group relative flex min-w-0 transition-colors duration-200 ease-out',
              isCard
                ? 'flex-col justify-between rounded-xl p-3 border border-slate-200/70'
                : 'items-center gap-2.5 rounded-lg px-3 py-2',
              compact && 'flex-none',
              loading
                ? 'bg-white/70'
                : isActive
                  ? cn('bg-transparent', isCard && 'border-transparent')
                  : isCard
                    ? cn('bg-white shadow-2xs hover:shadow-xs', onClick && colors.hoverCardBorder)
                    : cn(colors.background, onClick && colors.hover),
            )}
          >
            {onClick && (
              <button
                type="button"
                aria-label={actionLabel ?? `Lọc: ${label}`}
                {...(active !== undefined ? { 'aria-pressed': !loading && active } : {})}
                disabled={loading}
                onClick={onClick}
                className={cn(
                  'absolute inset-0 z-20 bg-transparent focus-visible:outline-none focus-visible:ring-2 cursor-pointer disabled:cursor-wait',
                  isCard ? 'rounded-xl' : 'rounded-lg',
                  colors.focusRing,
                )}
              />
            )}
            {isCard ? (
              <>
                <div className="relative z-10 pointer-events-none flex items-center justify-between gap-2 w-full mb-1">
                  <dd
                    className={cn(
                      'text-lg sm:text-xl font-bold leading-none tracking-tight tabular-nums',
                      colors.value,
                    )}
                  >
                    {loading ? <Skeleton className="h-5 w-8 max-w-full" /> : value}
                  </dd>
                  {icon && (
                    <span
                      aria-hidden="true"
                      className={cn(
                        'flex size-6 shrink-0 items-center justify-center rounded-lg transition-transform duration-200 ease-out [&_svg]:size-3.5',
                        onClick && !loading && 'group-hover:scale-105',
                        isActive && 'scale-105',
                        colors.icon,
                      )}
                    >
                      {loading ? <Skeleton className="size-3.5 rounded-sm" /> : icon}
                    </span>
                  )}
                </div>
                <dt
                  title={label}
                  className={cn(
                    'relative z-10 pointer-events-none text-[11px] font-semibold leading-tight line-clamp-2 text-slate-600',
                  )}
                >
                  {loading ? (
                    <>
                      <span className="invisible" aria-hidden="true">
                        {label}
                      </span>
                      <span className="sr-only">{label}</span>
                      <Skeleton className="absolute inset-x-0 top-1/2 h-2.5 -translate-y-1/2" />
                    </>
                  ) : (
                    label
                  )}
                </dt>
              </>
            ) : (
              <>
                {icon && !compact && (
                  <span
                    aria-hidden="true"
                    className={cn(
                      'relative z-10 pointer-events-none hidden size-7 shrink-0 items-center justify-center rounded-lg transition-transform duration-200 ease-out md:flex [&_svg]:size-3.5',
                      onClick && !loading && 'group-hover:scale-105',
                      isActive && 'scale-105',
                      colors.icon,
                    )}
                  >
                    {loading ? <Skeleton className="size-3.5 rounded-sm" /> : icon}
                  </span>
                )}
                <div className={cn('relative z-10 pointer-events-none flex min-w-0 flex-1', compact ? 'items-center gap-2' : 'flex-col')}>
                  <dt
                    title={label}
                    className={cn(
                      'relative order-2 mt-0.5 truncate text-[11px] font-medium transition-colors duration-200',
                      colors.label,
                      compact && 'order-none mt-0 text-sm',
                    )}
                  >
                    {loading ? (
                      <>
                        <span className="invisible" aria-hidden="true">
                          {label}
                        </span>
                        <span className="sr-only">{label}</span>
                        <Skeleton className="absolute inset-x-0 top-1/2 h-2.5 -translate-y-1/2" />
                      </>
                    ) : (
                      label
                    )}
                  </dt>
                  <dd
                    className={cn(
                      'order-1 text-base font-bold leading-tight tracking-tight tabular-nums',
                      colors.value,
                      compact && 'order-none text-sm',
                    )}
                  >
                    {loading ? <Skeleton className="h-4 w-8 max-w-full" /> : value}
                  </dd>
                </div>
              </>
            )}
          </div>
        );
      })}
    </dl>
  );
}
