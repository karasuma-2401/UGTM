import * as React from 'react';
import { Check, Minus } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface CheckboxProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type' | 'checked' | 'onChange'> {
  checked?: boolean | 'indeterminate';
  onCheckedChange?: (checked: boolean) => void;
  label?: React.ReactNode;
  description?: React.ReactNode;
}

const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  (
    {
      className,
      checked = false,
      onCheckedChange,
      disabled = false,
      label,
      description,
      id,
      ...props
    },
    ref,
  ) => {
    const generatedId = React.useId();
    const inputId = id || generatedId;
    const isIndeterminate = checked === 'indeterminate';
    const isChecked = checked === true;

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      if (disabled) return;
      onCheckedChange?.(e.target.checked);
    };

    return (
      <label
        htmlFor={inputId}
        className={cn(
          'inline-flex items-start gap-2.5 select-none',
          disabled ? 'cursor-not-allowed' : 'cursor-pointer',
          className,
        )}
      >
        <span className="relative flex shrink-0 items-center justify-center pt-0.5">
          <input
            type="checkbox"
            id={inputId}
            ref={ref}
            checked={isChecked}
            disabled={disabled}
            onChange={handleChange}
            className="peer sr-only"
            {...props}
          />
          <span
            className={cn(
              'h-[18px] w-[18px] shrink-0 rounded border flex items-center justify-center transition-all',
              'border-line-input bg-surface-card shadow-sm',
              'hover:border-primary',
              'peer-focus-visible:ring-2 peer-focus-visible:ring-primary/35 peer-focus-visible:ring-offset-1',
              (isChecked || isIndeterminate) &&
                'bg-brand-solid border-brand-solid text-white shadow-none',
              disabled &&
                'cursor-not-allowed opacity-50 bg-surface-muted border-line-subtle',
            )}
          >
            {isChecked && <Check className="size-3 stroke-[2.5]" />}
            {isIndeterminate && <Minus className="size-3 stroke-[2.5]" />}
          </span>
        </span>

        {(label || description) && (
          <span className="flex flex-col text-left">
            {label && (
              <span
                className={cn(
                  'text-sm font-medium leading-tight cursor-pointer text-ink-primary',
                  disabled && 'cursor-not-allowed opacity-60',
                )}
              >
                {label}
              </span>
            )}
            {description && (
              <span className="text-xs text-ink-muted mt-0.5 leading-normal">
                {description}
              </span>
            )}
          </span>
        )}
      </label>
    );
  },
);

Checkbox.displayName = 'Checkbox';

export { Checkbox };

