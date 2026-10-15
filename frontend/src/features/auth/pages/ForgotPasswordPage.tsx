import axios from 'axios';
import { ArrowLeft, ArrowRight, Mail, Shield, Smartphone } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { ApiResponse } from '@/shared/api/types';
import { startChallenge } from '../mocks/authMock';
import { startRecoveryApi } from '../services/authApi';
import type { OtpMethod } from '../types/auth.types';

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [identifier, setIdentifier] = useState('');
  const [method, setMethod] = useState<OtpMethod>('email');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const normalizedEmail = identifier.trim().includes('@')
    ? identifier.trim()
    : identifier.trim()
      ? `${identifier.trim()}@gm.uit.edu.vn`
      : '';

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = identifier.trim();
    if (!trimmed) return;

    if (/\s/.test(trimmed)) {
      setError('Vui lòng chỉ nhập địa chỉ Email hoặc MSSV (không nhập họ tên kèm khoảng trắng).');
      return;
    }

    setError('');
    setIsSubmitting(true);

    try {
      const emailToSend = normalizedEmail;
      const response = await startRecoveryApi(emailToSend);

      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể gửi mã xác thực. Vui lòng thử lại sau.');
      }

      const { challengeId, expiresAt } = response.data;

      sessionStorage.setItem('ugtm_recovery_challenge_id', challengeId);
      sessionStorage.setItem('ugtm_recovery_email', emailToSend);
      sessionStorage.setItem('ugtm_recovery_expires_at', expiresAt);

      startChallenge('reset', method);

      navigate('/verify-otp', {
        state: {
          challengeId,
          email: emailToSend,
          expiresAt,
        },
      });
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const apiError = err.response?.data as ApiResponse<unknown> | undefined;
        setError(apiError?.error?.message || err.message || 'Không thể kết nối đến máy chủ.');
      } else {
        setError((err as Error).message || 'Đã có lỗi xảy ra. Vui lòng thử lại.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="auth-card auth-forgot">
      <Link className="auth-back" to="/login">
        <ArrowLeft size={14} />
        Quay lại Đăng nhập
      </Link>
      <h1>Quên mật khẩu truy cập?</h1>
      <p className="auth-subtitle">
        Nhập Email trường hoặc MSSV để nhận mã OTP xác thực khôi phục mật khẩu tài khoản Khóa luận.
      </p>
      <form onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="recovery-id">EMAIL TÀI KHOẢN HOẶC MSSV</label>
          <Input
            id="recovery-id"
            required
            disabled={isSubmitting}
            placeholder="Nhập Email (ví dụ: huynhgiathinhst@gmail.com) hoặc MSSV..."
            leftIcon={<Mail size={16} />}
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            className="bg-white border-[#cbd7e8] h-[39px]"
          />
          {identifier.trim() && (
            <p className="auth-hint">
              Mã OTP sẽ được gửi về hộp thư: <strong>{normalizedEmail}</strong>
            </p>
          )}
        </div>
        <fieldset className="auth-methods">
          <legend>PHƯƠNG THỨC NHẬN MÃ XÁC THỰC OTP</legend>
          {(['email', 'sms'] as const).map((value) => (
            <label
              key={value}
              className={`auth-method ${method === value ? 'selected' : ''} ${value === 'sms' ? 'opacity-60 cursor-not-allowed' : ''}`}
            >
              <span className="auth-method-icon">
                {value === 'email' ? <Mail size={20} /> : <Smartphone size={20} />}
              </span>
              <span>
                <strong>
                  {value === 'email' ? 'Gửi mã qua Email trường' : 'Gửi mã qua tin nhắn SMS / Zalo'}
                </strong>
                {value === 'email' ? (
                  <small>Khuyên dùng</small>
                ) : (
                  <small className="text-amber-600 bg-amber-50 px-1.5 py-0.5 rounded ml-1 font-normal">Sắp hỗ trợ</small>
                )}
                <p>
                  {value === 'email'
                    ? (normalizedEmail || 'Hộp thư trường (@gm.uit.edu.vn / @uit.edu.vn)')
                    : 'Đang bảo trì'}
                </p>
              </span>
              <input
                type="radio"
                name="otp-method"
                value={value}
                disabled={value === 'sms' || isSubmitting}
                checked={method === value}
                onChange={() => setMethod(value)}
              />
            </label>
          ))}
        </fieldset>
        <div className="auth-warning">
          <Shield size={20} />
          <p>
            <strong>Lưu ý bảo mật:</strong> Tuyệt đối không chia sẻ mã OTP với bất kỳ ai, kể cả cán
            bộ quản trị hệ thống. Mã chỉ có hiệu lực trong vòng 10 phút.
          </p>
        </div>
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
          loadingText="Đang gửi mã OTP…"
          rightIcon={<ArrowRight size={16} />}
        >
          Gửi mã xác thực & Tiếp tục
        </Button>
      </form>
      <p className="auth-bottom">
        Bạn đã nhớ lại mật khẩu truy cập? <Link to="/login">Quay lại Đăng nhập ngay</Link>
      </p>
    </section>
  );
}
