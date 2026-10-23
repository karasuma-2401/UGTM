import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { Skeleton } from './skeleton';

const tones = {
  primary: {
    background: 'bg-primary/5',
    value: 'text-primary',
    label: 'text-slate-500',
    icon: 'text-primary',
  },
  warning: {
    background: 'bg-amber-50/80',
    value: 'text-amber-700',
    label: 'text-amber-700/80',
    icon: 'text-amber-600',
  },
  muted: {
    background: 'bg-slate-50',
    value: 'text-slate-600',
    label: 'text-slate-500',
    icon: 'text-slate-400',
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
}

export interface StatisticsBarProps {
  items: StatisticItem[];
  className?: string;
  loading?: boolean;
}

export function StatisticsBar({ items, className, loading = false }: StatisticsBarProps) {
  return (
    <dl
      aria-busy={loading}
      className={cn(
        'grid auto-cols-fr grid-flow-col gap-1 rounded-lg border border-slate-200/80 bg-white p-1 shadow-sm',
        className,
      )}
    >
      {items.map(({ key, label, value, icon, tone = 'muted', onClick, active }) => {
        const colors = tones[tone];
        return (
          <div
            key={key}
            className={cn(
              'relative flex min-w-0 items-center gap-2 rounded-md px-2.5 py-1.5',
              loading ? 'bg-slate-50' : colors.background,
              !loading && active && 'ring-1 ring-inset ring-primary/40',
            )}
          >
            {onClick && (
              <button
                type="button"
                aria-label={`Lọc: ${label}`}
                aria-pressed={!loading && Boolean(active)}
                disabled={loading}
                onClick={onClick}
                className="absolute inset-0 z-10 rounded-md transition-colors hover:bg-primary/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait"
              />
            )}
            {icon && (
              <span
                aria-hidden="true"
                className={cn('hidden shrink-0 md:block [&_svg]:size-3.5', colors.icon)}
              >
                {loading ? <Skeleton className="size-3.5 rounded-sm" /> : icon}
              </span>
            )}
            <div className="flex min-w-0 flex-1 flex-col">
              <dt
                title={label}
                className={cn(
                  'relative order-2 mt-1 truncate text-[10px] font-medium',
                  colors.label,
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
                  'order-1 text-base font-semibold leading-none tabular-nums',
                  colors.value,
                )}
              >
                {loading ? <Skeleton className="h-4 w-8 max-w-full" /> : value}
              </dd>
            </div>
          </div>
        );
      })}
    </dl>
  );
}
