import { AlertCircle, AlertTriangle, CheckCircle2, Info, X } from 'lucide-react';
import { useId, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

const themes = {
  error: {
    icon: AlertCircle,
    classes:
      'border-tone-danger-border bg-tone-danger-bg text-tone-danger-strong',
  },
  warning: {
    icon: AlertTriangle,
    classes:
      'border-tone-warning-border bg-tone-warning-bg text-tone-warning-strong',
  },
  info: {
    icon: Info,
    classes:
      'border-tone-info-border bg-tone-info-bg text-tone-info-strong',
  },
  success: {
    icon: CheckCircle2,
    classes:
      'border-tone-success-border bg-tone-success-bg text-tone-success-strong',
  },
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
          className="-m-1 flex size-9 shrink-0 items-center justify-center rounded-lg hover:bg-surface-hover/60 dark:hover:bg-surface-card/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"
        >
          <X aria-hidden="true" className="size-4" />
        </button>
      )}
    </div>
  );
}
