import axios from 'axios';
import { ArrowLeft, ArrowRight, LockKeyhole } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import type { ApiResponse } from '@/shared/api/types';
import { PasswordFields } from '../components/PasswordFields';
import { getChallenge, resetPasswordMock } from '../mocks/authMock';
import { resetRecoveryPasswordApi } from '../services/authApi';
import { validatePasswords } from '../utils/passwordValidation';

interface LocationState {
  resetGrant?: string;
  email?: string;
}

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const locationState = (location.state as LocationState) || {};

  // Lấy resetGrant từ location.state hoặc sessionStorage
  const resetGrant =
    locationState.resetGrant || sessionStorage.getItem('ugtm_recovery_reset_grant') || '';
  const challenge = getChallenge();

  const [password, setPassword] = useState('');
  const [confirmationPassword, setConfirmationPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Nếu không có resetGrant và cũng không có mock challenge thì quay lại trang Quên mật khẩu
  if (!resetGrant && (!challenge?.verified || challenge.purpose !== 'reset')) {
    return <Navigate to="/forgot-password" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);

    try {
      validatePasswords(password, confirmationPassword);

      if (resetGrant) {
        const response = await resetRecoveryPasswordApi(resetGrant, password);

        if (!response.success) {
          throw new Error(response.error?.message || 'Không thể đặt lại mật khẩu.');
        }

        sessionStorage.removeItem('ugtm_recovery_reset_grant');
        sessionStorage.removeItem('ugtm_recovery_email');
        sessionStorage.removeItem('ugtm_recovery_challenge_id');
        sessionStorage.removeItem('ugtm_recovery_expires_at');
      } else {
        resetPasswordMock(password);
      }

      navigate('/login', {
        replace: true,
        state: {
          success: 'Đặt mật khẩu mới thành công! Bạn có thể đăng nhập với mật khẩu mới vừa tạo.',
        },
      });
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const apiError = err.response?.data as ApiResponse<unknown> | undefined;
        setError(
          apiError?.error?.message || err.message || 'Không thể đặt lại mật khẩu. Vui lòng thử lại.',
        );
      } else {
        setError((err as Error).message || 'Đã có lỗi xảy ra khi lưu mật khẩu mới.');
      }
    } finally {
      setIsSubmitting(false);
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
        Hồ sơ đã được xác thực thành công. Tạo mật khẩu mới để tiếp tục truy cập Không gian Khóa luận.
      </p>
      <form onSubmit={handleSubmit}>
        <PasswordFields
          password={password}
          confirmationPassword={confirmationPassword}
          onPasswordChange={setPassword}
          onConfirmationChange={setConfirmationPassword}
          disabled={isSubmitting}
          showRules
          autoValidateMismatch
        />
        {error && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        <Button
          type="submit"
          className="auth-submit"
          disabled={isSubmitting}
          isLoading={isSubmitting}
          loadingText="Đang lưu mật khẩu mới…"
          rightIcon={<ArrowRight size={16} />}
        >
          Lưu mật khẩu mới
        </Button>
      </form>
    </section>
  );
}