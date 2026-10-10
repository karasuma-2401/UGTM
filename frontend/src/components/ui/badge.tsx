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
          'border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80',
        destructive:
          'border-transparent bg-destructive text-destructive-foreground shadow hover:bg-destructive/80',
        outline: 'text-foreground',
        success:
          'border-transparent bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
        warning:
          'border-transparent bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
        info: 'border-transparent bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

const dotVariantColors: Record<string, string> = {
  default: 'bg-primary-foreground',
  secondary: 'bg-secondary-foreground',
  destructive: 'bg-destructive-foreground',
  outline: 'bg-current',
  success: 'bg-emerald-600 dark:bg-emerald-400',
  warning: 'bg-amber-600 dark:bg-amber-400',
  info: 'bg-blue-600 dark:bg-blue-400',
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
  dot = false,
  leftIcon,
  rightIcon,
  children,
  ...props
}: BadgeProps) {
  const currentVariant = variant || 'default';
  const dotColor = dotVariantColors[currentVariant] || 'bg-current';

  return (
    <div className={cn(badgeVariants({ variant: currentVariant }), className)} {...props}>
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
