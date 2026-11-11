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
            'flex h-9 w-full rounded-lg border border-slate-200/90 bg-white px-3 py-1.5 text-sm text-slate-900 shadow-2xs transition-all duration-150 file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-slate-400 hover:border-slate-300 focus-visible:border-primary focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-primary/15 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:opacity-60',
            leftIcon && 'pl-9',
            rightIcon && 'pr-9',
            hasError &&
              'border-destructive text-destructive placeholder:text-destructive/60 focus-visible:border-destructive focus-visible:ring-destructive/15',
            className,
          )}
          ref={ref}
          disabled={disabled}
          aria-invalid={hasError ? 'true' : undefined}
          {...props}
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
          <p className="text-xs font-medium text-destructive mt-1.5" role="alert">
            {error}
          </p>
        ) : helperText ? (
          <p className="text-xs text-muted-foreground mt-1.5">{helperText}</p>
        ) : null}
      </div>
    );
  },
);
Input.displayName = 'Input';

export { Input };
