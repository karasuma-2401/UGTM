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
    <header className="relative overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
      <div
        aria-hidden="true"
        className="h-1 bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]"
      />
      <div className="flex flex-wrap items-center justify-between gap-4 px-5 py-5 sm:px-6">
        <div className="flex min-w-0 items-center gap-3.5">
          {icon && (
            <span
              aria-hidden="true"
              className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#2054a5] to-[#2563eb] text-white shadow-xs ring-4 ring-blue-50/80 [&_svg]:size-5"
            >
              {icon}
            </span>
          )}
          <div className="min-w-0">
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900">{title}</h1>
            {description && (
              <div className="text-xs sm:text-sm text-slate-500 mt-0.5 leading-relaxed">{description}</div>
            )}
          </div>
        </div>
        {children && <div className="flex flex-wrap items-center gap-2.5">{children}</div>}
      </div>
      {navigation && <div className="border-t border-slate-100/90 bg-slate-50/40 px-3 sm:px-4">{navigation}</div>}
    </header>
  );
}
