import { isStrongPassword } from '../utils/passwordValidation';

interface PasswordStrengthProps {
  password: string;
}

export function PasswordStrength({ password }: PasswordStrengthProps) {
  const isStrong = isStrongPassword(password);

  return (
    <div className={`auth-strength ${isStrong ? 'is-strong' : ''}`}>
      <div />
      <p>
        {isStrong
          ? '✓ Mật khẩu mạnh'
          : 'Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt; không chứa khoảng trắng.'}
      </p>
    </div>
  );
}
