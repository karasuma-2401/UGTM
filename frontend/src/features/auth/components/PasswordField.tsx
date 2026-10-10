import { Eye, EyeOff, LockKeyhole } from 'lucide-react';
import React, { useState, useId, type ReactNode } from 'react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

export interface PasswordFieldProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  'onChange'
> {
  id?: string;
  label?: ReactNode;
  value?: string;
  onChange?: ((value: string) => void) | React.ChangeEventHandler<HTMLInputElement>;
  onValueChange?: (value: string) => void;
  error?: string;
  helperText?: string;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  rightAction?: ReactNode;
  children?: ReactNode;
  showToggle?: boolean;
  containerClassName?: string;
}

export const PasswordField = React.forwardRef<HTMLInputElement, PasswordFieldProps>(
  (
    {
      id,
      label,
      value,
      onChange,
      onValueChange,
      autoComplete = 'new-password',
      disabled,
      placeholder = 'Nhập mật khẩu...',
      required = true,
      error,
      helperText,
      leftIcon = <LockKeyhole size={16} />,
      rightIcon,
      rightAction,
      children,
      showToggle = true,
      className,
      containerClassName,
      ...props
    },
    ref,
  ) => {
    const generatedId = useId();
    const inputId = id || generatedId;
    const [isVisible, setIsVisible] = useState(false);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      onValueChange?.(e.target.value);
      if (!onChange) return;
      try {
        (onChange as Function)(e.target.value);
      } catch {
        (onChange as Function)(e);
      }
    };

    const actionNode = rightAction || children;

    const toggleButton = showToggle ? (
      <button
        type="button"
        className={cn(
          'auth-eye text-slate-400 hover:text-slate-600 focus:outline-none transition-colors cursor-pointer',
          disabled && 'opacity-50 cursor-not-allowed hover:text-slate-400',
        )}
        onClick={() => setIsVisible((visible) => !visible)}
        aria-label={isVisible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
        aria-pressed={isVisible}
        disabled={disabled}
        tabIndex={-1}
      >
        {isVisible ? <EyeOff size={16} /> : <Eye size={16} />}
      </button>
    ) : null;

    return (
      <div className={cn('auth-field', containerClassName)}>
        {(label || actionNode) && (
          <div className="auth-label-row">
            {label ? <label htmlFor={inputId}>{label}</label> : <span />}
            {actionNode}
          </div>
        )}
        <Input
          ref={ref}
          id={inputId}
          type={isVisible ? 'text' : 'password'}
          autoComplete={autoComplete}
          required={required}
          disabled={disabled}
          placeholder={placeholder}
          value={value}
          onChange={handleChange}
          leftIcon={leftIcon}
          rightIcon={rightIcon !== undefined ? rightIcon : toggleButton}
          error={error}
          helperText={helperText}
          className={cn('bg-white border-[#cbd7e8] h-[39px]', className)}
          {...props}
        />
      </div>
    );
  },
);

PasswordField.displayName = 'PasswordField';
