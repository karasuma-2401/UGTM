import * as React from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: string;
  isInvalid?: boolean;
  helperText?: string;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
  containerClassName?: string;
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  (
    {
      className,
      containerClassName,
      type,
      error,
      isInvalid,
      helperText,
      leftIcon,
      rightIcon,
      disabled,
      ...props
    },
    ref,
  ) => {
    const hasError = Boolean(error || isInvalid);
    const descriptionId = React.useId();
    const describedBy = [props['aria-describedby'], error || helperText ? descriptionId : null].filter(Boolean).join(' ') || undefined;

    const inputElement = (
      <div className={cn('relative flex items-center w-full', containerClassName)}>
        {leftIcon && (
          <div className="absolute left-3 flex items-center justify-center text-muted-foreground pointer-events-none [&_svg]:size-4">
            {leftIcon}
          </div>
        )}
        <input
          type={type}
          className={cn(
            'flex h-9 w-full rounded-lg border border-line-input/90 bg-surface-card px-3 py-1.5 text-sm text-ink-primary shadow-2xs outline-none transition-[background-color,border-color,box-shadow] duration-150 file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-ink-muted hover:border-line-strong focus-visible:border-primary focus-visible:ring-2 focus-visible:ring-primary/15 disabled:cursor-not-allowed disabled:bg-surface-subtle disabled:opacity-60',
            leftIcon && 'pl-9',
            rightIcon && 'pr-9',
            hasError &&
              'border-destructive text-destructive placeholder:text-destructive/60 focus-visible:border-destructive focus-visible:ring-destructive/15 dark:text-rose-300 dark:border-rose-500/80',
            className,
          )}
          ref={ref}
          disabled={disabled}
          aria-invalid={hasError ? 'true' : undefined}
          {...props}
          aria-describedby={describedBy}
        />
        {rightIcon && (
          <div className="absolute right-3 flex items-center justify-center text-muted-foreground [&_svg]:size-4">
            {rightIcon}
          </div>
        )}
      </div>
    );

    if (!error && !helperText) {
      return inputElement;
    }

    return (
      <div className="w-full">
        {inputElement}
        {error ? (
          <p id={descriptionId} className="text-xs font-medium text-destructive mt-1.5" role="alert">
            {error}
          </p>
        ) : helperText ? (
          <p id={descriptionId} className="text-xs text-muted-foreground mt-1.5">{helperText}</p>
        ) : null}
      </div>
    );
  },
);
Input.displayName = 'Input';

export { Input };
