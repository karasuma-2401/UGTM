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
            'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
            leftIcon && 'pl-9',
            rightIcon && 'pr-9',
            hasError &&
              'border-destructive text-destructive placeholder:text-destructive/60 focus-visible:ring-destructive',
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
