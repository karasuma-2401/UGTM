import * as React from 'react';
import { cn } from '@/lib/utils';

export type RadioVariant = 'success' | 'primary' | 'warning' | 'destructive' | 'default';

export interface RadioOption<T = string> {
  value: T;
  label: React.ReactNode;
  rightText?: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  disabled?: boolean;
  variant?: RadioVariant;
}

interface RadioGroupContextValue<T = any> {
  name: string;
  value?: T;
  onChange?: (value: T) => void;
  disabled?: boolean;
  variant?: RadioVariant;
  selectedBorderColor?: string;
  selectedBgColor?: string;
}

const RadioGroupContext = React.createContext<RadioGroupContextValue | null>(null);

export interface RadioGroupProps<T = any> extends Omit<
  React.HTMLAttributes<HTMLDivElement>,
  'onChange' | 'defaultValue'
> {
  name?: string;
  value?: T;
  defaultValue?: T;
  onChange?: (value: T) => void;
  disabled?: boolean;
  variant?: RadioVariant;
  selectedBorderColor?: string;
  selectedBgColor?: string;
  orientation?: 'vertical' | 'horizontal';
  options?: RadioOption<T>[];
  children?: React.ReactNode;
}

export function RadioGroup<T = any>({
  name,
  value: controlledValue,
  defaultValue,
  onChange,
  disabled = false,
  variant = 'success',
  selectedBorderColor,
  selectedBgColor,
  orientation = 'vertical',
  options,
  children,
  className,
  ...props
}: RadioGroupProps<T>) {
  const generatedName = React.useId();
  const groupName = name || generatedName;

  const [uncontrolledValue, setUncontrolledValue] = React.useState<T | undefined>(defaultValue);

  const isControlled = controlledValue !== undefined;
  const currentValue = isControlled ? controlledValue : uncontrolledValue;

  const handleValueChange = React.useCallback(
    (val: T) => {
      if (disabled) return;
      if (!isControlled) {
        setUncontrolledValue(val);
      }
      onChange?.(val);
    },
    [disabled, isControlled, onChange],
  );

  const contextValue = React.useMemo<RadioGroupContextValue<T>>(
    () => ({
      name: groupName,
      value: currentValue,
      onChange: handleValueChange,
      disabled,
      variant,
      selectedBorderColor,
      selectedBgColor,
    }),
    [
      groupName,
      currentValue,
      handleValueChange,
      disabled,
      variant,
      selectedBorderColor,
      selectedBgColor,
    ],
  );

  return (
    <RadioGroupContext.Provider value={contextValue}>
      <div
        role="radiogroup"
        className={cn(
          'flex gap-2.5 w-full',
          orientation === 'vertical' ? 'flex-col' : 'flex-row flex-wrap',
          className,
        )}
        {...props}
      >
        {options
          ? options.map((opt) => (
              <RadioButton
                key={String(opt.value)}
                value={opt.value}
                label={opt.label}
                rightText={opt.rightText}
                description={opt.description}
                icon={opt.icon}
                disabled={opt.disabled}
                variant={opt.variant}
              />
            ))
          : children}
      </div>
    </RadioGroupContext.Provider>
  );
}

export interface RadioButtonProps<T = any> extends Omit<
  React.HTMLAttributes<HTMLLabelElement>,
  'onChange'
> {
  value: T;
  label?: React.ReactNode;
  rightText?: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  disabled?: boolean;
  checked?: boolean;
  onChange?: (checked: boolean) => void;
  variant?: RadioVariant;
  selectedBorderColor?: string;
  selectedBgColor?: string;
  radioColor?: string;
  name?: string;
}

export const RadioButton = React.forwardRef<HTMLLabelElement, RadioButtonProps>(
  (
    {
      value,
      label,
      rightText,
      description,
      icon,
      disabled: itemDisabled,
      checked: controlledChecked,
      onChange,
      variant: itemVariant,
      selectedBorderColor: itemSelectedBorderColor,
      selectedBgColor: itemSelectedBgColor,
      radioColor,
      name: itemName,
      className,
      style,
      ...props
    },
    ref,
  ) => {
    const groupContext = React.useContext(RadioGroupContext);

    const isGrouped = Boolean(groupContext);
    const isChecked = isGrouped ? groupContext?.value === value : Boolean(controlledChecked);
    const isDisabled = Boolean(itemDisabled || groupContext?.disabled);

    const groupVariant = groupContext?.variant || 'success';
    const effectiveVariant = itemVariant || groupVariant;

    const groupBorderColor = groupContext?.selectedBorderColor;
    const groupBgColor = groupContext?.selectedBgColor;

    const customBorder = itemSelectedBorderColor || groupBorderColor;
    const customBg = itemSelectedBgColor || groupBgColor;

    const handleClick = () => {
      if (isDisabled) return;
      if (isGrouped) {
        groupContext?.onChange?.(value);
      }
      onChange?.(true);
    };

    // Styling based on variant
    const getVariantStyles = () => {
      if (!isChecked) {
        return 'border-slate-200 bg-white hover:border-slate-300 dark:border-slate-800 dark:bg-slate-900';
      }

      switch (effectiveVariant) {
        case 'success':
          return 'border-[#91edc5] bg-[#eefbf5] text-[#087c5a] dark:border-emerald-700/60 dark:bg-emerald-950/30 dark:text-emerald-300';
        case 'primary':
          return 'border-primary/40 bg-blue-50/60 text-primary dark:border-blue-700/60 dark:bg-blue-950/30';
        case 'warning':
          return 'border-amber-300 bg-amber-50/70 text-amber-900 dark:border-amber-700/60 dark:bg-amber-950/30';
        case 'destructive':
          return 'border-destructive/40 bg-red-50/70 text-destructive dark:border-red-700/60 dark:bg-red-950/30';
        default:
          return 'border-slate-400 bg-slate-50 text-slate-900 dark:border-slate-600 dark:bg-slate-800';
      }
    };

    const containerStyle: React.CSSProperties = {
      ...style,
      ...(isChecked && customBorder ? { borderColor: customBorder } : {}),
      ...(isChecked && customBg ? { backgroundColor: customBg } : {}),
    };

    return (
      <label
        ref={ref}
        onClick={handleClick}
        style={containerStyle}
        className={cn(
          'relative flex items-center justify-between gap-3 px-3.5 py-2.5 rounded-xl border transition-all duration-200 select-none cursor-pointer',
          getVariantStyles(),
          isDisabled &&
            'opacity-50 cursor-not-allowed pointer-events-none bg-slate-50 border-slate-200',
          className,
        )}
        {...props}
      >
        <input
          type="radio"
          name={itemName || groupContext?.name}
          value={String(value)}
          checked={isChecked}
          disabled={isDisabled}
          onChange={() => {}} // handled by label click
          className="sr-only"
          tabIndex={-1}
        />

        {/* Left side: Radio circle + optional icon + label */}
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          {/* Custom Radio Button Indicator */}
          <span
            className={cn(
              'size-4 rounded-full flex items-center justify-center shrink-0 transition-colors',
              isChecked
                ? 'bg-primary'
                : 'border-2 border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-800',
            )}
            style={isChecked && radioColor ? { backgroundColor: radioColor } : undefined}
          >
            {isChecked && <span className="size-1.5 rounded-full bg-white shrink-0" />}
          </span>

          {/* Optional Icon slot */}
          {icon && (
            <span className="flex items-center justify-center shrink-0 [&_svg]:size-4">{icon}</span>
          )}

          {/* Title and optional description below */}
          <div className="min-w-0 truncate">
            {label && (
              <span
                className={cn(
                  'text-sm font-medium text-slate-800 dark:text-slate-100 block truncate',
                  isChecked &&
                    effectiveVariant === 'success' &&
                    'text-[#087c5a] dark:text-emerald-300',
                )}
              >
                {label}
              </span>
            )}
            {description && (
              <span className="text-xs text-slate-500 dark:text-slate-400 block truncate mt-0.5">
                {description}
              </span>
            )}
          </div>
        </div>

        {/* Right side: Optional subtitle / right text */}
        {rightText && (
          <span
            className={cn(
              'text-sm text-slate-600 dark:text-slate-400 shrink-0 ml-2 font-normal',
              isChecked &&
                effectiveVariant === 'success' &&
                'text-[#087c5a] dark:text-emerald-400 font-medium',
            )}
          >
            {rightText}
          </span>
        )}
      </label>
    );
  },
);

RadioButton.displayName = 'RadioButton';

// Aliases
export const Radio = RadioButton;
export const RadioCard = RadioButton;
