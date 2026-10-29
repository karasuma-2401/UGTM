import type { ReactNode } from 'react';

interface PageHeaderProps {
  title: string;
  description?: ReactNode;
  icon?: ReactNode;
  children?: ReactNode;
  navigation?: ReactNode;
}

export function PageHeader({ title, description, icon, children, navigation }: PageHeaderProps) {
  return (
    <header className="relative overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm">
      <div
        aria-hidden="true"
        className="h-1 bg-[linear-gradient(90deg,#2054a5_0%,#2054a5_20%,#fa7921_100%)]"
      />
      <div className="flex flex-wrap items-center justify-between gap-4 px-5 py-5 sm:px-6">
        <div className="flex min-w-0 items-center gap-3">
          {icon && (
            <span
              aria-hidden="true"
              className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#2054a5] text-white shadow-sm [&_svg]:size-5"
            >
              {icon}
            </span>
          )}
          <div className="min-w-0">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">{title}</h1>
            {description && (
              <div className="text-xs text-slate-500 mt-0.5">{description}</div>
            )}
          </div>
        </div>
        {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
      </div>
      {navigation && <div className="px-3 sm:px-4">{navigation}</div>}
    </header>
  );
}
