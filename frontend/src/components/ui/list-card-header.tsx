import type { ReactNode } from 'react';
import { CardHeader, CardTitle } from './card';
import { cn } from '@/lib/utils';

interface ListCardHeaderProps {
  title: ReactNode;
  icon: ReactNode;
  description?: ReactNode;
  children?: ReactNode;
  className?: string;
}

export function ListCardHeader({
  title,
  icon,
  description,
  children,
  className,
}: ListCardHeaderProps) {
  return (
    <CardHeader
      className={cn(
        'grid min-h-[104px] grid-cols-1 items-center gap-4 space-y-0 border-b border-slate-100 bg-gradient-to-r from-blue-50/70 via-white to-white px-5 py-4 xl:grid-cols-[minmax(0,1fr)_560px]',
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-3">
        <span
          aria-hidden="true"
          className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-blue-100 bg-white text-primary shadow-sm [&_svg]:size-5"
        >
          {icon}
        </span>
        <div className="min-w-0 space-y-1">
          <CardTitle className="text-base text-slate-900">{title}</CardTitle>
          {description != null && <p className="text-xs text-slate-500">{description}</p>}
        </div>
      </div>
      {children}
    </CardHeader>
  );
}
