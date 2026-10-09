import { LockKeyhole } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { AuthErrorMessage } from '../components/AuthErrorMessage';
import { AuthSubmitButton } from '../components/AuthSubmitButton';
import { BackToLoginLink } from '../components/BackToLoginLink';
import { PasswordFields } from '../components/PasswordFields';
import { getChallenge, resetPasswordMock } from '../mocks/authMock';
import { validatePasswords } from '../utils/passwordValidation';

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [password, setPassword] = useState('');
  const [confirmationPassword, setConfirmationPassword] = useState('');
  const [error, setError] = useState('');
  const challenge = getChallenge();
  if (!challenge?.verified || challenge.purpose !== 'reset')
    return <Navigate to="/forgot-password" replace />;
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    try {
      validatePasswords(password, confirmationPassword);
      resetPasswordMock(password);
      navigate('/login', {
        replace: true,
        state: { success: 'Đặt mật khẩu mới thành công. Hãy đăng nhập với mật khẩu vừa tạo.' },
      });
    } catch (err) {
      setError((err as Error).message);
    }
  }
  return (
    <section className="auth-card auth-reset">
      <BackToLoginLink />
      <div className="auth-shield">
        <LockKeyhole size={24} />
      </div>
      <h1>Thiết lập mật khẩu mới</h1>
      <p className="auth-subtitle">
        Hồ sơ đã được xác thực. Tạo mật khẩu mới để tiếp tục truy cập Không gian Khóa luận.
      </p>
      <form onSubmit={handleSubmit}>
        <PasswordFields
          password={password}
          confirmationPassword={confirmationPassword}
          onPasswordChange={setPassword}
          onConfirmationChange={setConfirmationPassword}
        />
        <AuthErrorMessage message={error} />
        <AuthSubmitButton>Lưu mật khẩu mới</AuthSubmitButton>
      </form>
    </section>
  );
}
