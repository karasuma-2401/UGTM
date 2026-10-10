import { ArrowLeft, ArrowRight, LockKeyhole } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
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
      <Link className="auth-back" to="/login">
        <ArrowLeft size={14} />
        Quay lại Đăng nhập
      </Link>
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
        {error && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        <Button type="submit" className="auth-submit" rightIcon={<ArrowRight size={16} />}>
          Lưu mật khẩu mới
        </Button>
      </form>
    </section>
  );
}
