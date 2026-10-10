import type { ReactNode } from 'react';
import { PasswordField } from './PasswordField';
import { PasswordStrength } from './PasswordStrength';

export interface PasswordFieldsProps {
  password: string;
  confirmationPassword: string;
  onPasswordChange?: (value: string) => void;
  onConfirmationChange?: (value: string) => void;
  disabled?: boolean;
  required?: boolean;

  passwordLabel?: ReactNode;
  confirmationLabel?: ReactNode;

  passwordPlaceholder?: string;
  confirmationPlaceholder?: string;

  passwordId?: string;
  confirmationId?: string;
  passwordName?: string;
  confirmationName?: string;

  passwordError?: string;
  confirmationError?: string;
  passwordHelperText?: string;
  confirmationHelperText?: string;
  autoValidateMismatch?: boolean;

  leftIcon?: ReactNode;
  passwordLeftIcon?: ReactNode;
  confirmationLeftIcon?: ReactNode;

  hideStrength?: boolean;
  showRules?: boolean;
  strongLabel?: ReactNode;
  weakLabel?: ReactNode;

  className?: string;
  fieldClassName?: string;
  containerClassName?: string;
}

export function PasswordFields({
  password,
  confirmationPassword,
  onPasswordChange,
  onConfirmationChange,
  disabled = false,
  required = true,
  passwordLabel = 'Thiết lập Mật khẩu mới',
  confirmationLabel = 'Xác nhận lại Mật khẩu',
  passwordPlaceholder = 'Nhập mật khẩu...',
  confirmationPlaceholder = 'Nhập lại mật khẩu để xác nhận...',
  passwordId = 'new-password',
  confirmationId = 'confirm-password',
  passwordName = 'new-password',
  confirmationName = 'confirm-password',
  passwordError,
  confirmationError,
  passwordHelperText,
  confirmationHelperText,
  autoValidateMismatch = false,
  leftIcon,
  passwordLeftIcon,
  confirmationLeftIcon,
  hideStrength = false,
  showRules = false,
  strongLabel,
  weakLabel,
  className,
  fieldClassName,
  containerClassName,
}: PasswordFieldsProps) {
  const effectiveConfirmationError =
    confirmationError ||
    (autoValidateMismatch && confirmationPassword.length > 0 && password !== confirmationPassword
      ? 'Mật khẩu xác nhận không khớp'
      : undefined);

  const content = (
    <>
      <PasswordField
        id={passwordId}
        name={passwordName}
        label={passwordLabel}
        value={password}
        onChange={onPasswordChange}
        disabled={disabled}
        required={required}
        placeholder={passwordPlaceholder}
        error={passwordError}
        helperText={passwordHelperText}
        leftIcon={passwordLeftIcon !== undefined ? passwordLeftIcon : leftIcon}
        className={fieldClassName}
        containerClassName={containerClassName}
        autoComplete="new-password"
      />

      {!hideStrength && (
        <PasswordStrength
          password={password}
          showRules={showRules}
          strongLabel={strongLabel}
          weakLabel={weakLabel}
        />
      )}

      <PasswordField
        id={confirmationId}
        name={confirmationName}
        label={confirmationLabel}
        value={confirmationPassword}
        onChange={onConfirmationChange}
        disabled={disabled}
        required={required}
        placeholder={confirmationPlaceholder}
        error={effectiveConfirmationError}
        helperText={confirmationHelperText}
        leftIcon={confirmationLeftIcon !== undefined ? confirmationLeftIcon : leftIcon}
        className={fieldClassName}
        containerClassName={containerClassName}
        autoComplete="new-password"
      />
    </>
  );

  if (className) {
    return <div className={className}>{content}</div>;
  }

  return content;
}
