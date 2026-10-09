import { PasswordField } from './PasswordField';
import { PasswordStrength } from './PasswordStrength';

interface PasswordFieldsProps {
  password: string;
  confirmationPassword: string;
  onPasswordChange: (value: string) => void;
  onConfirmationChange: (value: string) => void;
}

export function PasswordFields({
  password,
  confirmationPassword,
  onPasswordChange,
  onConfirmationChange,
}: PasswordFieldsProps) {
  return (
    <>
      <PasswordField
        id="new-password"
        label="Thiết lập Mật khẩu mới"
        value={password}
        onChange={onPasswordChange}
      />
      <PasswordStrength password={password} />
      <PasswordField
        id="confirm-password"
        label="Xác nhận lại Mật khẩu"
        value={confirmationPassword}
        onChange={onConfirmationChange}
      />
    </>
  );
}
