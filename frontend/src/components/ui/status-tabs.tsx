import { useRef, useState, useLayoutEffect, useEffect, useCallback, type ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { Skeleton } from './skeleton';

export interface StatusTabItem<T extends string = string> {
  key: T;
  label: string;
  count?: number | string;
  dotColor?: string;
  icon?: ReactNode;
  tone?: 'primary' | 'warning' | 'muted' | 'danger';
  activeBadgeClass?: string;
  inactiveBadgeClass?: string;
}

export interface StatusTabsProps<T extends string = string> {
  tabs: StatusTabItem<T>[];
  activeKey: T | 'none' | '';
  onSelect: (key: T) => void;
  ariaLabel?: string;
  loading?: boolean;
  className?: string;
}

const defaultToneClasses = {
  primary: {
    activeBadge: 'bg-blue-100 text-primary',
    inactiveBadge: 'bg-slate-200/80 text-slate-700',
    dot: 'bg-blue-600',
  },
  warning: {
    activeBadge: 'bg-amber-100 text-amber-800',
    inactiveBadge: 'bg-amber-50 text-amber-700 border border-amber-200/60',
    dot: 'bg-amber-500',
  },
  danger: {
    activeBadge: 'bg-rose-100 text-rose-800',
    inactiveBadge: 'bg-rose-50 text-rose-700 border border-rose-200/60',
    dot: 'bg-rose-500',
  },
  muted: {
    activeBadge: 'bg-slate-200 text-slate-900',
    inactiveBadge: 'bg-slate-100 text-slate-600',
    dot: 'bg-slate-400',
  },
};

export function StatusTabs<T extends string = string>({
  tabs,
  activeKey,
  onSelect,
  ariaLabel = 'Lọc trạng thái',
  loading = false,
  className,
}: StatusTabsProps<T>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  const [indicatorStyle, setIndicatorStyle] = useState<{
    left: number;
    top: number;
    width: number;
    height: number;
    ready: boolean;
  }>({ left: 0, top: 0, width: 0, height: 0, ready: false });

  const updateIndicator = useCallback(() => {
    const container = containerRef.current;
    if (!container) return;

    if (activeKey === 'none') {
      setIndicatorStyle((prev) => ({ ...prev, ready: false }));
      return;
    }

    const currentTab = tabRefs.current[activeKey];
    if (!currentTab) {
      setIndicatorStyle((prev) => ({ ...prev, ready: false }));
      return;
    }

    const containerRect = container.getBoundingClientRect();
    const tabRect = currentTab.getBoundingClientRect();

    setIndicatorStyle({
      left: tabRect.left - containerRect.left,
      top: tabRect.top - containerRect.top,
      width: tabRect.width,
      height: tabRect.height,
      ready: true,
    });
  }, [activeKey]);

  useLayoutEffect(() => {
    updateIndicator();
  }, [updateIndicator, tabs]);

  useEffect(() => {
    const handleResize = () => updateIndicator();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [updateIndicator]);

  return (
    <div
      ref={containerRef}
      role="tablist"
      aria-label={ariaLabel}
      className={cn(
        'relative isolate inline-flex flex-wrap items-center gap-1 p-1 rounded-xl bg-slate-100/90 border border-slate-200/70',
        className,
      )}
    >
      {/* Nền trượt animate phần cứng bằng translate3d & width (220ms snap) */}
      {indicatorStyle.ready && (
        <span
          aria-hidden="true"
          style={{
            transform: `translate3d(${indicatorStyle.left}px, ${indicatorStyle.top}px, 0)`,
            width: `${indicatorStyle.width}px`,
            height: `${indicatorStyle.height}px`,
            transition:
              'transform 220ms cubic-bezier(0.16, 1, 0.3, 1), width 220ms cubic-bezier(0.16, 1, 0.3, 1)',
          }}
          className="absolute top-0 left-0 z-0 rounded-lg bg-white shadow-2xs border border-slate-200/90 ring-1 ring-slate-950/5 pointer-events-none motion-reduce:transition-none"
        />
      )}

      {tabs.map((tab) => {
        const isActive = activeKey === tab.key;
        const toneStyle = defaultToneClasses[tab.tone ?? 'muted'];
        const activeBadge = tab.activeBadgeClass ?? toneStyle.activeBadge;
        const inactiveBadge = tab.inactiveBadgeClass ?? toneStyle.inactiveBadge;
        const dotColor = tab.dotColor ?? (tab.icon ? undefined : toneStyle.dot);

        return (
          <button
            key={tab.key}
            ref={(el) => {
              tabRefs.current[tab.key] = el;
            }}
            data-selection-item
            role="tab"
            type="button"
            aria-selected={isActive}
            onClick={() => onSelect(tab.key)}
            className={cn(
              'relative z-10 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold select-none cursor-pointer',
              'transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40',
              isActive
                ? 'text-slate-950'
                : 'text-slate-600 hover:text-slate-900',
            )}
          >
            {tab.icon && <span className="shrink-0">{tab.icon}</span>}
            {dotColor && (
              <span
                aria-hidden="true"
                className={cn(
                  'size-2 rounded-full shrink-0 transition-transform duration-200',
                  dotColor,
                  isActive && 'scale-110',
                )}
              />
            )}

            <span className="whitespace-nowrap">{tab.label}</span>

            {/* Badge hiển thị số lượng */}
            <span
              className={cn(
                'ml-1 inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-bold tabular-nums transition-colors duration-200',
                isActive ? activeBadge : inactiveBadge,
              )}
            >
              {loading && tab.count === undefined ? (
                <Skeleton className="h-3 w-4 rounded-sm" />
              ) : tab.count !== undefined ? (
                typeof tab.count === 'number' ? tab.count.toLocaleString('vi-VN') : tab.count
              ) : (
                '—'
              )}
            </span>
          </button>
        );
      })}
    </div>
  );
}
