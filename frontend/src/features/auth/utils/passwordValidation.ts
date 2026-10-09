const MIN_PASSWORD_LENGTH = 12;

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
