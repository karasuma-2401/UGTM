import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { Skeleton } from './skeleton';

const tones = {
  primary: {
    background: 'bg-blue-50/55',
    value: 'text-primary',
    label: 'text-slate-600',
    icon: 'text-primary bg-blue-100/70',
    activeRing: 'ring-primary/30',
    activeCardBorder: 'border-primary/40',
    hoverCardBorder: 'hover:border-blue-300/80',
    hover: 'hover:bg-primary/5',
    focusRing: 'focus-visible:ring-ring',
  },
  success: {
    background: 'bg-emerald-50/65',
    value: 'text-emerald-700',
    label: 'text-emerald-800/80',
    icon: 'text-emerald-600 bg-emerald-100/70',
    activeRing: 'ring-emerald-300',
    activeCardBorder: 'border-emerald-500/40',
    hoverCardBorder: 'hover:border-emerald-300/80',
    hover: 'hover:bg-emerald-100/40',
    focusRing: 'focus-visible:ring-emerald-500',
  },
  warning: {
    background: 'bg-amber-50/65',
    value: 'text-amber-600',
    label: 'text-amber-800/80',
    icon: 'text-amber-600 bg-amber-100/70',
    activeRing: 'ring-amber-300',
    activeCardBorder: 'border-amber-500/40',
    hoverCardBorder: 'hover:border-amber-300/80',
    hover: 'hover:bg-amber-100/40',
    focusRing: 'focus-visible:ring-amber-500',
  },
  muted: {
    background: 'bg-slate-50/80',
    value: 'text-slate-700',
    label: 'text-slate-500',
    icon: 'text-slate-500 bg-slate-200/60',
    activeRing: 'ring-primary/30',
    activeCardBorder: 'border-primary/40',
    hoverCardBorder: 'hover:border-slate-300',
    hover: 'hover:bg-primary/5',
    focusRing: 'focus-visible:ring-ring',
  },
  danger: {
    background: 'bg-rose-50/80',
    value: 'text-rose-700',
    label: 'text-rose-700',
    icon: 'text-rose-600 bg-rose-100/70',
    activeRing: 'ring-rose-300',
    activeCardBorder: 'border-rose-500/40',
    hoverCardBorder: 'hover:border-rose-300/80',
    hover: 'hover:bg-rose-100/40',
    focusRing: 'focus-visible:ring-rose-500',
  },
};

export interface StatisticItem {
  key: string;
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  tone?: keyof typeof tones;
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

  return (
    <dl
      aria-busy={loading}
      className={cn(
        isCard
          ? 'grid gap-2 rounded-2xl border border-slate-200/80 bg-slate-50/50 p-2'
          : 'grid auto-cols-fr grid-flow-col gap-1.5 rounded-xl border border-slate-200/80 bg-slate-100/60 p-1.5',
        className,
        compact && 'flex flex-wrap border-0 bg-transparent p-0 gap-2',
      )}
    >
      {items.map(({ key, label, value, icon, tone = 'muted', onClick, active, actionLabel }) => {
        const colors = tones[tone];
        return (
          <div
            key={key}
            className={cn(
              'relative flex min-w-0 transition-all duration-150',
              isCard
                ? 'flex-col justify-between rounded-xl p-3 border border-slate-200/70'
                : 'items-center gap-2.5 rounded-lg px-3 py-2',
              compact && 'flex-none',
              loading
                ? 'bg-white/70'
                : active
                  ? cn(
                      'bg-white shadow-xs ring-1',
                      colors.activeRing,
                      isCard && cn(colors.activeCardBorder, 'shadow-xs'),
                    )
                  : isCard
                    ? cn('bg-white shadow-2xs hover:shadow-xs', onClick && colors.hoverCardBorder)
                    : colors.background,
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
                  'absolute inset-0 z-10 transition-colors focus-visible:outline-none focus-visible:ring-2 cursor-pointer disabled:cursor-wait',
                  isCard ? 'rounded-xl' : 'rounded-lg',
                  colors.hover,
                  colors.focusRing,
                )}
              />
            )}
            {isCard ? (
              <>
                <div className="flex items-center justify-between gap-2 w-full mb-1">
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
                        'flex size-6 shrink-0 items-center justify-center rounded-lg [&_svg]:size-3.5',
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
                    'relative text-[11px] font-semibold leading-tight line-clamp-2 text-slate-600',
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
                      'hidden size-7 shrink-0 items-center justify-center rounded-lg md:flex [&_svg]:size-3.5',
                      colors.icon,
                    )}
                  >
                    {loading ? <Skeleton className="size-3.5 rounded-sm" /> : icon}
                  </span>
                )}
                <div className={cn('flex min-w-0 flex-1', compact ? 'items-center gap-2' : 'flex-col')}>
                  <dt
                    title={label}
                    className={cn(
                      'relative order-2 mt-0.5 truncate text-[11px] font-medium',
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
