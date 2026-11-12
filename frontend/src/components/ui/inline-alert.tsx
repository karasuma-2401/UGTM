import { AlertCircle, AlertTriangle, CheckCircle2, Info, X } from 'lucide-react';
import { useId, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

const themes = {
  error: { icon: AlertCircle, classes: 'border-rose-200 bg-rose-50 text-rose-900' },
  warning: { icon: AlertTriangle, classes: 'border-amber-200 bg-amber-50 text-amber-900' },
  info: { icon: Info, classes: 'border-blue-200 bg-blue-50 text-blue-900' },
  success: { icon: CheckCircle2, classes: 'border-emerald-200 bg-emerald-50 text-emerald-900' },
};

export function InlineAlert({
  tone = 'error',
  title,
  children,
  action,
  onDismiss,
  className,
}: {
  tone?: keyof typeof themes;
  title?: string;
  children: ReactNode;
  action?: ReactNode;
  onDismiss?: () => void;
  className?: string;
}) {
  const id = useId();
  const { icon: Icon, classes } = themes[tone];
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      aria-labelledby={title ? id : undefined}
      className={cn('flex items-start gap-3 rounded-xl border p-4 text-sm', classes, className)}
    >
      <Icon className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
      <div className="min-w-0 flex-1 [overflow-wrap:anywhere]">
        {title && (
          <p id={id} className="font-semibold">
            {title}
          </p>
        )}
        <div className={cn('leading-relaxed', title && 'mt-1')}>{children}</div>
        {action && <div className="mt-3 flex flex-wrap gap-2">{action}</div>}
      </div>
      {onDismiss && (
        <button
          type="button"
          aria-label="Đóng thông báo"
          onClick={onDismiss}
          className="-m-1 flex size-9 shrink-0 items-center justify-center rounded-lg hover:bg-white/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"
        >
          <X aria-hidden="true" className="size-4" />
        </button>
      )}
    </div>
  );
}
