import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { Check, ChevronDown, Search, X } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface SelectOption<T = string> {
  value: T;
  label: React.ReactNode;
  disabled?: boolean;
  description?: string;
  icon?: React.ReactNode;
  badge?: React.ReactNode;
}

export type RawOption<T = string> = SelectOption<T> | string | number;

const selectTriggerVariants = cva(
  'flex w-full items-center justify-between rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50 text-left',
  {
    variants: {
      size: {
        default: 'h-9 py-2',
        sm: 'h-8 px-2.5 py-1 text-xs',
        lg: 'h-10 px-3.5 py-2 text-base',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  },
);

export interface SelectProps<T = string>
  extends
    Omit<React.HTMLAttributes<HTMLDivElement>, 'onChange' | 'defaultValue'>,
    VariantProps<typeof selectTriggerVariants> {
  options: RawOption<T>[];
  value?: T | T[];
  defaultValue?: T | T[];
  onChange?: (value: any, option?: SelectOption<T> | SelectOption<T>[]) => void;
  placeholder?: string;
  disabled?: boolean;
  readOnly?: boolean;
  error?: string;
  isInvalid?: boolean;
  helperText?: string;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
  searchable?: boolean;
  searchPlaceholder?: string;
  noOptionsMessage?: string;
  clearable?: boolean;
  multiple?: boolean;
  triggerClassName?: string;
  contentClassName?: string;
  optionClassName?: string;
  containerClassName?: string;
  maxMenuHeight?: number | string;
}

export function Select<T = string>({
  options = [],
  value: controlledValue,
  defaultValue,
  onChange,
  placeholder = 'Chọn một tùy chọn...',
  disabled = false,
  readOnly = false,
  error,
  isInvalid,
  helperText,
  leftIcon,
  rightIcon,
  searchable = false,
  searchPlaceholder = 'Tìm kiếm tùy chọn...',
  noOptionsMessage = 'Không tìm thấy kết quả',
  clearable = false,
  multiple = false,
  size,
  className,
  triggerClassName,
  contentClassName,
  optionClassName,
  containerClassName,
  maxMenuHeight = 260,
  ...props
}: SelectProps<T>) {
  const [isOpen, setIsOpen] = React.useState(false);
  const [searchQuery, setSearchQuery] = React.useState('');
  const [uncontrolledValue, setUncontrolledValue] = React.useState<T | T[] | undefined>(
    defaultValue !== undefined ? defaultValue : multiple ? [] : undefined,
  );

  const containerRef = React.useRef<HTMLDivElement>(null);
  const searchInputRef = React.useRef<HTMLInputElement>(null);

  const isControlled = controlledValue !== undefined;
  const currentValue = isControlled ? controlledValue : uncontrolledValue;

  const hasError = Boolean(error || isInvalid);

  const normalizedOptions = React.useMemo<SelectOption<T>[]>(() => {
    return options.map((opt) => {
      if (typeof opt === 'string' || typeof opt === 'number') {
        return { value: opt as unknown as T, label: String(opt) };
      }
      return opt;
    });
  }, [options]);

  const filteredOptions = React.useMemo(() => {
    if (!searchQuery.trim()) return normalizedOptions;
    const query = searchQuery.toLowerCase().trim();
    return normalizedOptions.filter((opt) => {
      const labelText =
        typeof opt.label === 'string'
          ? opt.label
          : typeof opt.label === 'number'
            ? String(opt.label)
            : String(opt.value);
      const descText = opt.description || '';
      return labelText.toLowerCase().includes(query) || descText.toLowerCase().includes(query);
    });
  }, [normalizedOptions, searchQuery]);

  React.useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSearchQuery('');
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  React.useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && isOpen) {
        setIsOpen(false);
        setSearchQuery('');
      }
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  React.useEffect(() => {
    if (isOpen && searchable && searchInputRef.current) {
      setTimeout(() => {
        searchInputRef.current?.focus();
      }, 50);
    }
  }, [isOpen, searchable]);

  const handleToggle = () => {
    if (disabled || readOnly) return;
    setIsOpen((prev) => !prev);
    if (isOpen) {
      setSearchQuery('');
    }
  };

  const isOptionSelected = (optValue: T) => {
    if (multiple && Array.isArray(currentValue)) {
      return currentValue.includes(optValue);
    }
    return currentValue === optValue;
  };

  const handleSelectOption = (option: SelectOption<T>, e?: React.MouseEvent) => {
    e?.stopPropagation();
    if (option.disabled || disabled || readOnly) return;

    if (multiple) {
      const currentArr = Array.isArray(currentValue) ? [...currentValue] : [];
      const index = currentArr.indexOf(option.value);
      let nextArr: T[];
      if (index > -1) {
        nextArr = currentArr.filter((v) => v !== option.value);
      } else {
        nextArr = [...currentArr, option.value];
      }

      if (!isControlled) {
        setUncontrolledValue(nextArr);
      }
      const selectedOptions = normalizedOptions.filter((o) => nextArr.includes(o.value));
      onChange?.(nextArr, selectedOptions);
    } else {
      if (!isControlled) {
        setUncontrolledValue(option.value);
      }
      onChange?.(option.value, option);
      setIsOpen(false);
      setSearchQuery('');
    }
  };

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (disabled || readOnly) return;

    const clearedValue = multiple ? [] : undefined;
    if (!isControlled) {
      setUncontrolledValue(clearedValue);
    }
    onChange?.(clearedValue as any, multiple ? [] : undefined);
    setSearchQuery('');
  };

  const renderTriggerContent = () => {
    if (multiple && Array.isArray(currentValue) && currentValue.length > 0) {
      const selectedLabels = normalizedOptions
        .filter((opt) => currentValue.includes(opt.value))
        .map((opt) => opt.label);

      if (selectedLabels.length === 1) {
        return <span className="text-foreground truncate">{selectedLabels[0]}</span>;
      }
      return (
        <span className="flex items-center gap-1.5 truncate">
          <span className="text-foreground font-medium truncate">{selectedLabels[0]}</span>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded-full text-[11px] font-semibold bg-primary/10 text-primary">
            +{selectedLabels.length - 1}
          </span>
        </span>
      );
    }

    if (!multiple && currentValue !== undefined && currentValue !== null && currentValue !== '') {
      const selectedOpt = normalizedOptions.find((opt) => opt.value === currentValue);
      if (selectedOpt) {
        return (
          <span className="flex items-center gap-2 text-foreground truncate">
            {selectedOpt.icon && (
              <span className="[&_svg]:size-4 text-muted-foreground shrink-0">
                {selectedOpt.icon}
              </span>
            )}
            <span className="truncate">{selectedOpt.label}</span>
            {selectedOpt.badge && <span className="ml-1 shrink-0">{selectedOpt.badge}</span>}
          </span>
        );
      }
    }

    return <span className="text-muted-foreground truncate">{placeholder}</span>;
  };

  const hasSelectedValue = multiple
    ? Array.isArray(currentValue) && currentValue.length > 0
    : currentValue !== undefined && currentValue !== null && currentValue !== '';

  return (
    <div
      ref={containerRef}
      className={cn('relative w-full text-left', containerClassName, className)}
      {...props}
    >
      <button
        type="button"
        disabled={disabled}
        onClick={handleToggle}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-invalid={hasError ? 'true' : undefined}
        className={cn(
          selectTriggerVariants({ size }),
          hasError && 'border-destructive text-destructive focus:ring-destructive',
          triggerClassName,
        )}
      >
        <div className="flex items-center gap-2 truncate flex-1 mr-2">
          {leftIcon && (
            <span className="text-muted-foreground [&_svg]:size-4 shrink-0 flex items-center justify-center">
              {leftIcon}
            </span>
          )}
          {renderTriggerContent()}
        </div>

        <div className="flex items-center gap-1 shrink-0 text-muted-foreground">
          {clearable && hasSelectedValue && !disabled && !readOnly && (
            <span
              role="button"
              tabIndex={-1}
              onClick={handleClear}
              className="rounded-sm p-0.5 hover:text-foreground hover:bg-slate-100 transition-colors cursor-pointer"
              aria-label="Xóa lựa chọn"
            >
              <X className="size-3.5" />
            </span>
          )}

          {rightIcon !== undefined ? (
            rightIcon
          ) : (
            <ChevronDown
              className={cn(
                'size-4 text-muted-foreground transition-transform duration-200',
                isOpen && 'rotate-180',
              )}
            />
          )}
        </div>
      </button>

      {isOpen && (
        <div
          role="listbox"
          tabIndex={-1}
          style={{ maxHeight: maxMenuHeight }}
          className={cn(
            'absolute z-50 left-0 right-0 mt-1 w-full rounded-md border border-slate-200 bg-white p-1 text-slate-900 shadow-md outline-none overflow-hidden flex flex-col animate-in fade-in-0 zoom-in-95',
            contentClassName,
          )}
        >
          {searchable && (
            <div className="flex items-center border-b border-slate-100 px-2.5 py-1.5 mb-1 shrink-0">
              <Search className="size-4 text-slate-400 mr-2 shrink-0" />
              <input
                ref={searchInputRef}
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder={searchPlaceholder}
                className="w-full bg-transparent text-sm text-slate-800 placeholder:text-slate-400 outline-none"
                onClick={(e) => e.stopPropagation()}
              />
              {searchQuery && (
                <button
                  type="button"
                  onClick={() => setSearchQuery('')}
                  className="text-slate-400 hover:text-slate-600 p-0.5"
                >
                  <X className="size-3.5" />
                </button>
              )}
            </div>
          )}

          <div className="overflow-y-auto flex-1 space-y-0.5">
            {filteredOptions.length === 0 ? (
              <div className="py-6 text-center text-xs text-slate-400">{noOptionsMessage}</div>
            ) : (
              filteredOptions.map((opt) => {
                const isSelected = isOptionSelected(opt.value);
                return (
                  <div
                    key={String(opt.value)}
                    role="option"
                    aria-selected={isSelected}
                    aria-disabled={opt.disabled}
                    onClick={(e) => handleSelectOption(opt, e)}
                    className={cn(
                      'relative flex items-center justify-between gap-2 px-2.5 py-2 text-sm rounded-md select-none transition-colors cursor-pointer',
                      isSelected
                        ? 'bg-blue-50/80 text-primary font-medium'
                        : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900',
                      opt.disabled && 'opacity-50 cursor-not-allowed pointer-events-none',
                      optionClassName,
                    )}
                  >
                    <div className="flex items-center gap-2 truncate flex-1">
                      {opt.icon && (
                        <span className="[&_svg]:size-4 text-slate-400 shrink-0">{opt.icon}</span>
                      )}
                      <div className="truncate">
                        <div className="truncate">{opt.label}</div>
                        {opt.description && (
                          <div className="text-[11px] text-slate-400 font-normal truncate mt-0.5">
                            {opt.description}
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-1.5 shrink-0">
                      {opt.badge && <span>{opt.badge}</span>}
                      {isSelected && <Check className="size-4 text-primary shrink-0" />}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}

      {error ? (
        <p className="text-xs font-medium text-destructive mt-1.5" role="alert">
          {error}
        </p>
      ) : helperText ? (
        <p className="text-xs text-muted-foreground mt-1.5">{helperText}</p>
      ) : null}
    </div>
  );
}

export function Combobox<T = string>(props: SelectProps<T>) {
  return <Select searchable {...props} />;
}
