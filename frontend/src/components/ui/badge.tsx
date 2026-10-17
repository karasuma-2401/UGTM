import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center rounded-md border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground shadow hover:bg-primary/80',
        secondary:
          'border-transparent bg-slate-100 text-slate-700 hover:bg-slate-200/80 dark:bg-slate-800 dark:text-slate-300',
        destructive:
          'border-transparent bg-destructive text-destructive-foreground shadow hover:bg-destructive/80',
        outline: 'text-foreground',
        success:
          'border-transparent bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300',
        warning:
          'border-transparent bg-amber-50 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300',
        info: 'border-transparent bg-blue-50 text-blue-700 dark:bg-blue-950/60 dark:text-blue-300',
        active:
          'border-transparent bg-purple-50 text-purple-700 dark:bg-purple-950/60 dark:text-purple-300',
        inactive:
          'border-transparent bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
        error:
          'border-transparent bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300',
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
  default: 'bg-primary-foreground',
  secondary: 'bg-slate-400 dark:bg-slate-500',
  destructive: 'bg-destructive-foreground',
  outline: 'bg-current',
  success: 'bg-emerald-500 dark:bg-emerald-400',
  warning: 'bg-amber-500 dark:bg-amber-400',
  info: 'bg-blue-500 dark:bg-blue-400',
  active: 'bg-purple-500 dark:bg-purple-400',
  inactive: 'bg-slate-400 dark:bg-slate-500',
  error: 'bg-rose-500 dark:bg-rose-400',
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
      {dot && <span className={cn('size-3 rounded-sm mr-1.5 shrink-0', dotColor)} />}
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
