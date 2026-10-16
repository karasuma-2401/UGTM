import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

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
}

export interface StatisticsBarProps {
  items: StatisticItem[];
  className?: string;
}

export function StatisticsBar({ items, className }: StatisticsBarProps) {
  return (
    <dl
      className={cn(
        'grid auto-cols-fr grid-flow-col gap-1 rounded-lg border border-slate-200/80 bg-white p-1 shadow-sm',
        className,
      )}
    >
      {items.map(({ key, label, value, icon, tone = 'muted' }) => {
        const colors = tones[tone];
        return (
          <div
            key={key}
            className={cn('flex items-center gap-2 rounded-md px-2.5 py-1.5', colors.background)}
          >
            {icon && (
              <span
                aria-hidden="true"
                className={cn('hidden shrink-0 md:block [&_svg]:size-3.5', colors.icon)}
              >
                {icon}
              </span>
            )}
            <div className="flex min-w-0 flex-col">
              <dt className={cn('order-2 mt-1 text-[10px] font-medium', colors.label)}>{label}</dt>
              <dd
                className={cn(
                  'order-1 text-base font-semibold leading-none tabular-nums',
                  colors.value,
                )}
              >
                {value}
              </dd>
            </div>
          </div>
        );
      })}
    </dl>
  );
}
