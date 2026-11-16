import { Badge, type BadgeProps } from './badge';
import { cn } from '@/lib/utils';

export type StatusTone = 'neutral' | 'info' | 'warning' | 'success' | 'error' | 'orange';

const appearances: Record<
  StatusTone,
  { variant: NonNullable<BadgeProps['variant']>; className: string }
> = {
  neutral: {
    variant: 'secondary',
    className:
      'border-line-input/80 bg-surface-muted/80 text-ink-body',
  },
  info: {
    variant: 'info',
    className:
      'border-tone-info-border bg-tone-info-bg text-tone-info-text',
  },
  warning: {
    variant: 'warning',
    className:
      'border-tone-warning-border bg-tone-warning-bg text-tone-warning-strong',
  },
  success: {
    variant: 'success',
    className:
      'border-tone-success-border bg-tone-success-bg text-tone-success-text',
  },
  error: {
    variant: 'error',
    className:
      'border-tone-danger-border bg-tone-danger-bg text-tone-danger-text',
  },
  orange: {
    variant: 'warning',
    className:
      'border-tone-orange-border bg-tone-orange-bg text-tone-orange-text',
  },
};

export interface StatusBadgeProps extends Omit<BadgeProps, 'variant' | 'dot'> {
  tone?: StatusTone;
}

export function StatusBadge({ tone = 'neutral', className, ...props }: StatusBadgeProps) {
  const appearance = appearances[tone];
  return (
    <Badge
      {...props}
      variant={appearance.variant}
      rounded="full"
      dot
      className={cn(
        'whitespace-nowrap px-2.5 py-0.5 text-xs font-medium shadow-none',
        appearance.className,
        className,
      )}
    />
  );
}
