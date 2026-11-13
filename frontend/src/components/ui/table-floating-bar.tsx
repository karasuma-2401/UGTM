import { useEffect, type HTMLAttributes } from 'react';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from './button';

export interface TableFloatingBarProps extends HTMLAttributes<HTMLDivElement> {
  selectedCount: number;
  totalCount?: number;
  onClearSelection?: () => void;
  show?: boolean;
}

export function TableFloatingBar({
  selectedCount,
  totalCount,
  onClearSelection,
  show,
  children,
  className,
  ...props
}: TableFloatingBarProps) {
  const isVisible = show ?? selectedCount > 0;

  useEffect(() => {
    if (!isVisible || !onClearSelection) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClearSelection?.();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isVisible, onClearSelection]);

  if (!isVisible) return null;

  return (
    <div
      role="region"
      aria-label="Thao tác hàng loạt trên các mục đã chọn"
      className={cn(
        'fixed bottom-6 left-1/2 -translate-x-1/2 z-40 flex flex-wrap items-center gap-2 sm:gap-3 rounded-2xl bg-white/95 px-4 py-2.5 shadow-xl border border-slate-200/90 backdrop-blur-md ring-1 ring-slate-900/5 animate-in fade-in-0 slide-in-from-bottom-4 duration-200',
        className,
      )}
      {...props}
    >
      <div className="flex items-center gap-2 pr-2 border-r border-slate-200/80">
        <span className="flex size-2 rounded-full bg-primary animate-pulse" />
        <span className="text-xs font-semibold text-slate-700">
          Đã chọn{' '}
          <strong className="font-bold text-primary tabular-nums">{selectedCount}</strong>
          {totalCount !== undefined && (
            <span className="text-slate-400 font-normal"> / {totalCount}</span>
          )}
        </span>
      </div>

      <div className="flex items-center gap-1.5">{children}</div>

      {onClearSelection && (
        <div className="pl-1 border-l border-slate-200/80">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onClearSelection}
            className="h-8 px-2 text-xs text-slate-500 hover:text-slate-800 rounded-lg cursor-pointer"
            aria-label="Bỏ chọn tất cả các dòng"
          >
            <X className="size-3.5 mr-1" />
            Bỏ chọn
          </Button>
        </div>
      )}
    </div>
  );
}
