import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export interface InfoCellProps {
  title: ReactNode;
  description?: ReactNode;
  leading?: ReactNode;
  trailing?: ReactNode;
  className?: string;
  titleClassName?: string;
  descriptionClassName?: string;
}

export function InfoCell({
  title,
  description,
  leading,
  trailing,
  className,
  titleClassName,
  descriptionClassName,
}: InfoCellProps) {
  return (
    <div className={cn('flex items-center gap-3', className)}>
      {leading && <div className="shrink-0">{leading}</div>}
      <div className="min-w-0 flex-1 text-left">
        <div
          className={cn(
            'flex flex-wrap items-center gap-2 font-semibold text-slate-800',
            titleClassName,
          )}
        >
          {title}
          {trailing}
        </div>
        {description != null && (
          <div className={cn('mt-1 break-words text-xs text-slate-500', descriptionClassName)}>
            {description}
          </div>
        )}
      </div>
    </div>
  );
}
