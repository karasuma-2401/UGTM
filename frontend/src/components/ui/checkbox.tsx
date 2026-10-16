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
              'border-[var(--color-border-input,#cbd7e8)] bg-[var(--color-bg-white,#ffffff)] shadow-sm',
              'hover:border-[var(--color-primary,#2563eb)]',
              'peer-focus-visible:ring-2 peer-focus-visible:ring-[var(--color-primary-focus,#2663ff)] peer-focus-visible:ring-offset-1',
              (isChecked || isIndeterminate) &&
                'bg-[var(--color-primary,#2563eb)] border-[var(--color-primary,#2563eb)] text-white shadow-none',
              disabled &&
                'cursor-not-allowed opacity-50 bg-[var(--color-bg-input-muted,#f1f4f8)] border-[var(--color-border-input-muted,#eef0f4)]',
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
                  'text-sm font-medium leading-tight cursor-pointer text-[var(--color-text-main,#10182c)]',
                  disabled && 'cursor-not-allowed opacity-60',
                )}
              >
                {label}
              </span>
            )}
            {description && (
              <span className="text-xs text-[var(--color-text-desc,#667b9a)] mt-0.5 leading-normal">
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

