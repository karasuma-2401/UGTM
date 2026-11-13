import { Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';

export function PendingState({
  label = 'Đang tải dữ liệu…',
  className,
}: {
  label?: string;
  className?: string;
}) {
  return (
    <div
      role="status"
      aria-busy="true"
      className={cn('flex items-center gap-2.5 py-3 text-sm text-slate-600', className)}
    >
      <Loader2 className="size-4 animate-spin" aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}
