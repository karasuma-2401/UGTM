import React, { useRef, useState, useLayoutEffect, useEffect, useCallback } from 'react';
import { UsersRound } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';

export interface UserStatusTabsProps {
  activeKey: string; // '' | 'PendingActivation' | 'Disabled' | 'none'
  onSelect: (status: '' | 'PendingActivation' | 'Disabled') => void;
  stats?: {
    total?: number;
    pendingActivation?: number;
    disabled?: number;
  };
  loading?: boolean;
}

interface StatusTabConfig {
  key: '' | 'PendingActivation' | 'Disabled';
  label: string;
  count?: number;
  dotColor?: string;
  icon?: React.ReactNode;
  activeBadgeClass: string;
  inactiveBadgeClass: string;
}

export const UserStatusTabs: React.FC<UserStatusTabsProps> = ({
  activeKey,
  onSelect,
  stats,
  loading = false,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  const [indicatorStyle, setIndicatorStyle] = useState<{
    left: number;
    top: number;
    width: number;
    height: number;
    ready: boolean;
  }>({ left: 0, top: 0, width: 0, height: 0, ready: false });

  const tabs: StatusTabConfig[] = [
    {
      key: '',
      label: 'Tổng tài khoản',
      count: stats?.total,
      icon: <UsersRound className="size-3.5 text-slate-500" />,
      activeBadgeClass: 'bg-blue-100 text-primary',
      inactiveBadgeClass: 'bg-slate-200/80 text-slate-600',
    },
    {
      key: 'PendingActivation',
      label: 'Chờ kích hoạt',
      count: stats?.pendingActivation,
      dotColor: 'bg-amber-500',
      activeBadgeClass: 'bg-amber-100 text-amber-800',
      inactiveBadgeClass: 'bg-amber-50 text-amber-700 border border-amber-200/60',
    },
    {
      key: 'Disabled',
      label: 'Vô hiệu hóa',
      count: stats?.disabled,
      dotColor: 'bg-rose-500',
      activeBadgeClass: 'bg-rose-100 text-rose-800',
      inactiveBadgeClass: 'bg-rose-50 text-rose-700 border border-rose-200/60',
    },
  ];

  // Cập nhật vị trí thẻ trượt mượt mà theo GPU
  const updateIndicator = useCallback(() => {
    const container = containerRef.current;
    if (!container) return;

    if (activeKey === 'none') {
      setIndicatorStyle((prev) => ({ ...prev, ready: false }));
      return;
    }

    const currentTab = tabRefs.current[activeKey];
    if (!currentTab) return;

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
  }, [updateIndicator, stats?.total, stats?.pendingActivation, stats?.disabled]);

  useEffect(() => {
    const handleResize = () => updateIndicator();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [updateIndicator]);

  return (
    <div
      ref={containerRef}
      role="tablist"
      aria-label="Lọc nhanh trạng thái tài khoản"
      className="relative isolate inline-flex flex-wrap items-center gap-1 p-1 rounded-xl bg-slate-100/90 border border-slate-200/70"
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
            {tab.icon && <span>{tab.icon}</span>}
            {tab.dotColor && (
              <span
                aria-hidden="true"
                className={cn(
                  'size-2 rounded-full shrink-0 transition-transform duration-200',
                  tab.dotColor,
                  isActive && 'scale-110',
                )}
              />
            )}

            <span className="truncate">{tab.label}</span>

            {/* Badge hiển thị số lượng tài khoản */}
            <span
              className={cn(
                'ml-1 inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-bold tabular-nums transition-colors duration-200',
                isActive ? tab.activeBadgeClass : tab.inactiveBadgeClass,
              )}
            >
              {loading && stats === undefined ? (
                <Skeleton className="h-3 w-4 rounded-sm" />
              ) : tab.count !== undefined ? (
                tab.count.toLocaleString('vi-VN')
              ) : (
                '—'
              )}
            </span>
          </button>
        );
      })}
    </div>
  );
};
