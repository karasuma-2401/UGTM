import type { ComponentType, ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { SelectionIndicator } from './selection-indicator';
import { cn } from '@/lib/utils';

export interface TabItem {
  id: string;
  label: string;
  icon?: ComponentType<{ className?: string }>;
  href?: string;
  count?: number | string;
  badge?: ReactNode;
}

export interface NavigationTabsProps {
  tabs: TabItem[];
  activeId: string;
  onSelect?: (id: string) => void;
  ariaLabel?: string;
  continuityKey?: string;
  className?: string;
  variant?: 'underline' | 'pill';
}

export function NavigationTabs({
  tabs,
  activeId,
  onSelect,
  ariaLabel = 'Điều hướng',
  continuityKey = 'nav-tabs',
  className,
  variant = 'underline',
}: NavigationTabsProps) {
  let navigate: ReturnType<typeof useNavigate> | null = null;
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    navigate = useNavigate();
  } catch {
    navigate = null;
  }

  return (
    <nav
      aria-label={ariaLabel}
      className={cn('relative isolate flex overflow-x-auto', className)}
    >
      <SelectionIndicator
        activeKey={activeId}
        selector='[data-selection-item][aria-current="page"]'
        continuityKey={continuityKey}
        className={
          variant === 'underline'
            ? "bg-primary/5 after:absolute after:inset-x-0 after:bottom-0 after:h-0.5 after:content-[''] after:[background-image:var(--gradient-brand)]"
            : 'rounded-lg bg-white shadow-xs border border-slate-200/80'
        }
      />
      {tabs.map((item) => {
        const isActive = activeId === item.id;
        const Icon = item.icon;

        const content = (
          <>
            {Icon && <Icon className="size-4 shrink-0" aria-hidden="true" />}
            <span className="truncate">{item.label}</span>
            {item.count !== undefined && (
              <span
                className={cn(
                  'ml-1.5 inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-bold tabular-nums transition-colors',
                  isActive
                    ? 'bg-primary/15 text-primary'
                    : 'bg-slate-200/70 text-slate-600',
                )}
              >
                {item.count}
              </span>
            )}
            {item.badge}
          </>
        );

        const commonClass = cn(
          'relative z-10 inline-flex h-12 min-w-0 flex-1 items-center justify-center gap-2 border-b-2 border-transparent px-3 text-center text-xs font-semibold transition-colors duration-150 sm:flex-none sm:px-5 sm:text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring select-none cursor-pointer',
          isActive
            ? 'text-primary'
            : 'border-transparent text-slate-500 hover:bg-slate-100/60 hover:text-slate-800',
        );

        if (item.href) {
          return (
            <Link
              key={item.id}
              data-selection-item
              to={item.href}
              aria-current={isActive ? 'page' : undefined}
              onClick={(event) => {
                onSelect?.(item.id);
                if (
                  event.button !== 0 ||
                  event.metaKey ||
                  event.ctrlKey ||
                  event.shiftKey ||
                  event.altKey
                )
                  return;
                if (navigate && item.href) {
                  event.preventDefault();
                  void navigate(item.href, { flushSync: true });
                }
              }}
              className={commonClass}
            >
              {content}
            </Link>
          );
        }

        return (
          <button
            key={item.id}
            type="button"
            data-selection-item
            onClick={() => onSelect?.(item.id)}
            aria-current={isActive ? 'page' : undefined}
            className={commonClass}
          >
            {content}
          </button>
        );
      })}
    </nav>
  );
}
