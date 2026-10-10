import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const progressTrackVariants = cva(
  'w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden relative',
  {
    variants: {
      size: {
        xs: 'h-1',
        sm: 'h-1.5',
        default: 'h-2',
        lg: 'h-3',
        xl: 'h-4',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  },
);

const progressIndicatorVariants = cva(
  'h-full rounded-full transition-all duration-500 ease-out flex items-center justify-center',
  {
    variants: {
      variant: {
        default: 'bg-primary',
        success: 'bg-emerald-600',
        warning: 'bg-amber-500',
        destructive: 'bg-destructive',
        info: 'bg-sky-500',
        gradient: 'bg-gradient-to-r from-blue-600 to-indigo-600',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

export interface ProgressBarProps
  extends
    React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof progressTrackVariants>,
    VariantProps<typeof progressIndicatorVariants> {
  value?: number;
  min?: number;
  max?: number;
  label?: React.ReactNode;
  showValue?: boolean;
  valueText?: React.ReactNode;
  valueFormat?: (percentage: number, value: number) => React.ReactNode;
  description?: React.ReactNode;
  indicatorColor?: string;
  trackColor?: string;
  height?: number | string;
  animated?: boolean;
  indeterminate?: boolean;
  trackClassName?: string;
  indicatorClassName?: string;
  labelClassName?: string;
  valueClassName?: string;
}

export const ProgressBar = React.forwardRef<HTMLDivElement, ProgressBarProps>(
  (
    {
      value = 0,
      min = 0,
      max = 100,
      label,
      showValue = true,
      valueText,
      valueFormat,
      description,
      variant = 'default',
      size = 'default',
      indicatorColor,
      trackColor,
      height,
      animated = true,
      indeterminate = false,
      className,
      trackClassName,
      indicatorClassName,
      labelClassName,
      valueClassName,
      style,
      ...props
    },
    ref,
  ) => {
    const rawPercentage = max > min ? ((value - min) / (max - min)) * 100 : 0;
    const percentage = Math.min(100, Math.max(0, rawPercentage));

    const hasHeader = Boolean(label || (showValue && value !== undefined) || valueText);

    const formattedValue =
      valueText !== undefined
        ? valueText
        : valueFormat
          ? valueFormat(percentage, value)
          : `${Math.round(percentage)}%`;

    const indicatorStyle: React.CSSProperties = {
      width: indeterminate ? '40%' : `${percentage}%`,
      ...(indicatorColor ? { backgroundColor: indicatorColor } : {}),
    };

    const trackStyle: React.CSSProperties = {
      ...(height !== undefined ? { height } : {}),
      ...(trackColor ? { backgroundColor: trackColor } : {}),
    };

    return (
      <div
        ref={ref}
        className={cn('w-full', className)}
        style={style}
        role="progressbar"
        aria-valuenow={indeterminate ? undefined : value}
        aria-valuemin={min}
        aria-valuemax={max}
        aria-label={typeof label === 'string' ? label : undefined}
        {...props}
      >
        {hasHeader && (
          <div className="flex items-center justify-between gap-3 mb-2">
            {label && (
              <span
                className={cn(
                  'text-sm font-medium text-slate-700 dark:text-slate-200 truncate',
                  labelClassName,
                )}
              >
                {label}
              </span>
            )}
            {(showValue || valueText !== undefined) && (
              <span
                className={cn(
                  'text-sm font-semibold text-slate-900 dark:text-slate-100 ml-auto shrink-0',
                  valueClassName,
                )}
              >
                {formattedValue}
              </span>
            )}
          </div>
        )}

        <div className={cn(progressTrackVariants({ size }), trackClassName)} style={trackStyle}>
          <div
            className={cn(
              progressIndicatorVariants({ variant }),
              !animated && 'transition-none',
              indeterminate && 'animate-indeterminate w-1/3 absolute',
              indicatorClassName,
            )}
            style={indicatorStyle}
          />
        </div>

        {description && <p className="text-xs text-muted-foreground mt-1.5">{description}</p>}
      </div>
    );
  },
);

ProgressBar.displayName = 'ProgressBar';

export const Progress = ProgressBar;