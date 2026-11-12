import React, { useRef, useState, useLayoutEffect, useEffect, useCallback } from 'react';
import { ScrollText } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';

export interface AuditOutcomeStats {
  total: number;
  success: number;
  denied: number;
  failed: number;
  other?: number;
}

interface AuditLogStatusTabsProps {
  currentOutcome: string;
  onSelectOutcome: (outcome: string) => void;
  stats: AuditOutcomeStats;
  loading?: boolean;
  error?: boolean;
}

interface StatusTabConfig {
  key: string;
  label: string;
  count: number;
  dotColor?: string;
  icon?: React.ReactNode;
  activeBadgeClass: string;
  inactiveBadgeClass: string;
}

export const AuditLogStatusTabs: React.FC<AuditLogStatusTabsProps> = ({
  currentOutcome,
  onSelectOutcome,
  stats,
  loading = false,
  error = false,
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
      label: 'Tất cả',
      count: stats.total,
      icon: <ScrollText className="size-3.5 text-slate-500" />,
      activeBadgeClass: 'bg-blue-100 text-primary',
      inactiveBadgeClass: 'bg-slate-200/80 text-slate-600',
    },
    {
      key: 'Success',
      label: 'Thành công',
      count: stats.success,
      dotColor: 'bg-emerald-500',
      activeBadgeClass: 'bg-emerald-100 text-emerald-800',
      inactiveBadgeClass: 'bg-emerald-50 text-emerald-700 border border-emerald-200/60',
    },
    {
      key: 'Denied',
      label: 'Bị từ chối',
      count: stats.denied,
      dotColor: 'bg-amber-500',
      activeBadgeClass: 'bg-amber-100 text-amber-800',
      inactiveBadgeClass: 'bg-amber-50 text-amber-700 border border-amber-200/60',
    },
    {
      key: 'Failed',
      label: 'Thất bại',
      count: stats.failed,
      dotColor: 'bg-rose-500',
      activeBadgeClass: 'bg-rose-100 text-rose-800',
      inactiveBadgeClass: 'bg-rose-50 text-rose-700 border border-rose-200/60',
    },
  ];

  // Tính toán vị trí chính xác của tab đang chọn để trượt mượt mà không dùng scale
  const updateIndicator = useCallback(() => {
    const container = containerRef.current;
    const currentTab = tabRefs.current[currentOutcome];
    if (!container || !currentTab) return;

    const containerRect = container.getBoundingClientRect();
    const tabRect = currentTab.getBoundingClientRect();

    setIndicatorStyle({
      left: tabRect.left - containerRect.left,
      top: tabRect.top - containerRect.top,
      width: tabRect.width,
      height: tabRect.height,
      ready: true,
    });
  }, [currentOutcome]);

  useLayoutEffect(() => {
    updateIndicator();
  }, [updateIndicator, stats.total, stats.success, stats.denied, stats.failed]);

  useEffect(() => {
    const handleResize = () => updateIndicator();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [updateIndicator]);

  return (
    <div
      ref={containerRef}
      role="group"
      aria-label="Lọc nhanh trạng thái sự kiện"
      className="relative isolate inline-flex flex-wrap items-center gap-1 p-1 rounded-xl bg-slate-100/90 border border-slate-200/70"
    >
      {/* Nền trượt animate phần cứng bằng translate3d & width (220ms snap) không méo góc/viền */}
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
        const isActive = currentOutcome === tab.key;

        return (
          <button
            key={tab.key}
            ref={(el) => {
              tabRefs.current[tab.key] = el;
            }}
            data-selection-item
            type="button"
            aria-pressed={isActive}
            onClick={() => onSelectOutcome(tab.key)}
            className={cn(
              // Giữ font-semibold cố định trên cả active và inactive để không bị giật kích thước chữ khi click
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

            {/* Badge hiển thị số lượng sự kiện */}
            <span
              className={cn(
                'ml-1 inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-bold tabular-nums transition-colors duration-200',
                isActive ? tab.activeBadgeClass : tab.inactiveBadgeClass,
              )}
            >
              {loading ? (
                <Skeleton className="h-3 w-4 rounded-sm" />
              ) : error ? (
                <span aria-label="Không tải được số lượng">—</span>
              ) : (
                tab.count.toLocaleString('vi-VN')
              )}
            </span>
          </button>
        );
      })}
      {!loading && !error && Boolean(stats.other) && <span className="px-2 text-xs text-slate-600">Khác: {stats.other?.toLocaleString('vi-VN')}</span>}
    </div>
  );
};
