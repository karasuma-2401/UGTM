import { Badge, type BadgeProps } from './badge';
import { cn } from '@/lib/utils';

export type StatusTone = 'neutral' | 'info' | 'warning' | 'success' | 'error' | 'orange';

const appearances: Record<
  StatusTone,
  { variant: NonNullable<BadgeProps['variant']>; className: string }
> = {
  neutral: { variant: 'secondary', className: 'border-slate-200/80 bg-slate-100/80 text-slate-600' },
  info: { variant: 'info', className: 'border-blue-200/70 bg-blue-50/90 text-blue-700' },
  warning: { variant: 'warning', className: 'border-amber-200/70 bg-amber-50/90 text-amber-800' },
  success: {
    variant: 'success',
    className: 'border-emerald-200/70 bg-emerald-50/90 text-emerald-700',
  },
  error: { variant: 'error', className: 'border-rose-200/70 bg-rose-50/90 text-rose-700' },
  orange: { variant: 'warning', className: 'border-orange-200/70 bg-orange-50/90 text-orange-700' },
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
