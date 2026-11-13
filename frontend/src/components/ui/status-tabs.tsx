import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { SelectionIndicator } from './selection-indicator';
import { SELECTION_TONES, type SelectionTone } from './selection-tones';
import { Skeleton } from './skeleton';

export interface StatusTabItem<T extends string = string> {
  key: T;
  label: string;
  count?: number | string;
  dotColor?: string;
  icon?: ReactNode;
  tone?: SelectionTone;
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

export function StatusTabs<T extends string = string>({
  tabs,
  activeKey,
  onSelect,
  ariaLabel = 'Lọc trạng thái',
  loading = false,
  className,
}: StatusTabsProps<T>) {
  const activeTab =
    activeKey === 'none' ? undefined : tabs.find((tab) => tab.key === activeKey);
  const activeColors = activeTab
    ? SELECTION_TONES[activeTab.tone ?? 'muted']
    : SELECTION_TONES.primary;

  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className={cn(
        'relative isolate inline-flex flex-wrap items-center gap-1.5 rounded-xl border border-slate-200/80 bg-slate-100/60 p-1.5',
        className,
      )}
    >
      <SelectionIndicator
        activeKey={activeTab?.key ?? null}
        selector='[data-selection-item][aria-selected="true"]'
        className={cn(
          'z-0 rounded-lg border-[1.5px] border-transparent shadow-xs',
          activeColors.activeIndicator,
        )}
      />

      {tabs.map((tab) => {
        const isActive = activeKey === tab.key;
        const toneStyle = SELECTION_TONES[tab.tone ?? 'muted'];
        const activeBadge = tab.activeBadgeClass ?? toneStyle.activeBadge;
        const inactiveBadge = tab.inactiveBadgeClass ?? toneStyle.inactiveBadge;
        const dotColor = tab.dotColor ?? (tab.icon ? undefined : toneStyle.dot);

        return (
          <button
            key={tab.key}
            data-selection-item
            role="tab"
            type="button"
            aria-selected={isActive}
            onClick={() => onSelect(tab.key)}
            className={cn(
              'relative inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold select-none cursor-pointer',
              'transition-colors duration-200 ease-out focus-visible:outline-none focus-visible:ring-2',
              toneStyle.focusRing,
              isActive
                ? 'bg-transparent text-slate-950'
                : cn(toneStyle.background, toneStyle.hover, 'text-slate-600 hover:text-slate-900'),
            )}
          >
            {tab.icon && <span className="relative z-10 shrink-0">{tab.icon}</span>}
            {dotColor && (
              <span
                aria-hidden="true"
                className={cn(
                  'relative z-10 size-2 shrink-0 rounded-full transition-transform duration-200',
                  dotColor,
                  isActive && 'scale-110',
                )}
              />
            )}

            <span className="relative z-10 whitespace-nowrap">{tab.label}</span>

            {/* Badge hiển thị số lượng */}
            <span
              className={cn(
                'relative z-10 ml-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-bold tabular-nums transition-colors duration-200',
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
