import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center rounded-md border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-brand-solid text-white shadow hover:bg-brand-solid-hover',
        secondary:
          'border-transparent bg-surface-muted text-ink-secondary hover:bg-surface-strong/80',
        destructive:
          'border-transparent bg-destructive text-destructive-foreground shadow hover:bg-destructive/80',
        outline: 'text-foreground',
        success:
          'border-transparent bg-tone-success-bg text-tone-success-text',
        warning:
          'border-transparent bg-tone-warning-bg text-tone-warning-strong',
        info: 'border-transparent bg-tone-info-bg text-tone-info-text',
        active:
          'border-transparent bg-tone-violet-bg text-tone-violet-text',
        inactive:
          'border-transparent bg-surface-muted text-ink-body',
        error:
          'border-transparent bg-tone-danger-bg text-tone-danger-text',
      },
      rounded: {
        default: 'rounded-md',
        none: 'rounded-none',
        sm: 'rounded-sm',
        md: 'rounded-md',
        lg: 'rounded-lg',
        full: 'rounded-full',
      },
    },
    defaultVariants: {
      variant: 'default',
      rounded: 'default',
    },
  },
);

const dotVariantColors: Record<string, string> = {
  default: 'bg-surface-card',
  secondary: 'bg-ink-subtle',
  destructive: 'bg-surface-card',
  outline: 'bg-current',
  success: 'bg-tone-success-icon',
  warning: 'bg-tone-warning-icon',
  info: 'bg-tone-info-icon',
  active: 'bg-tone-violet-icon',
  inactive: 'bg-ink-subtle',
  error: 'bg-tone-danger-icon',
};

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof badgeVariants> {
  dot?: boolean;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

function Badge({
  className,
  variant = 'default',
  rounded = 'default',
  dot = false,
  leftIcon,
  rightIcon,
  children,
  ...props
}: BadgeProps) {
  const currentVariant = variant || 'default';
  const dotColor = dotVariantColors[currentVariant] || 'bg-current';

  return (
    <div
      className={cn(badgeVariants({ variant: currentVariant, rounded }), className)}
      {...props}
    >
      {dot && <span className={cn('size-1.5 rounded-full mr-1.5 shrink-0', dotColor)} />}
      {leftIcon && (
        <span className="mr-1 inline-flex items-center shrink-0 [&_svg]:size-3">{leftIcon}</span>
      )}
      {children}
      {rightIcon && (
        <span className="ml-1 inline-flex items-center shrink-0 [&_svg]:size-3">{rightIcon}</span>
      )}
    </div>
  );
}

export { Badge, badgeVariants };
