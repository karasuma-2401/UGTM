const MIN_PASSWORD_LENGTH = 12;

export interface PasswordRuleStatus {
  id: string;
  label: string;
  passed: boolean;
}

export function checkPasswordCriteria(password: string): PasswordRuleStatus[] {
  return [
    {
      id: 'length',
      label: 'Tối thiểu 12 ký tự',
      passed: password.length >= MIN_PASSWORD_LENGTH,
    },
    {
      id: 'uppercase',
      label: 'Chữ in hoa (A-Z)',
      passed: /[A-Z]/.test(password),
    },
    {
      id: 'lowercase',
      label: 'Chữ in thường (a-z)',
      passed: /[a-z]/.test(password),
    },
    {
      id: 'number',
      label: 'Chữ số (0-9)',
      passed: /\d/.test(password),
    },
    {
      id: 'special',
      label: 'Ký tự đặc biệt (!@#$...)',
      passed: /[^A-Za-z0-9\s]/.test(password),
    },
    {
      id: 'no-space',
      label: 'Không chứa khoảng trắng',
      passed: password.length > 0 && !/\s/.test(password),
    },
  ];
}

export function isStrongPassword(value: string): boolean {
  return (
    value.length >= MIN_PASSWORD_LENGTH &&
    /[A-Z]/.test(value) &&
    /[a-z]/.test(value) &&
    /\d/.test(value) &&
    /[^A-Za-z0-9\s]/.test(value) &&
    !/\s/.test(value)
  );
}

export function validatePasswords(password: string, confirmation: string): void {
  if (!isStrongPassword(password)) {
    throw new Error('Mật khẩu mới chưa đạt yêu cầu bảo vệ.');
  }

  if (password !== confirmation) {
    throw new Error('Mật khẩu xác nhận không khớp.');
  }
}
