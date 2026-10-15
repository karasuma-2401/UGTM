import axios from 'axios';
import { ArrowLeft, ArrowRight, Check, Clock, LockKeyhole, Mail, Shield } from 'lucide-react';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import type { ApiResponse } from '@/shared/api/types';
import { MAX_OTP_ATTEMPTS, OTP_LENGTH } from '../constants/auth.constants';
import { getChallenge, resendChallenge, verifyOtp } from '../mocks/authMock';
import { startRecoveryApi, verifyRecoveryOtpApi } from '../services/authApi';

interface LocationState {
  challengeId?: string;
  email?: string;
  expiresAt?: string;
}

export function OtpPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const locationState = (location.state as LocationState) || {};

  // Lấy thông tin challenge từ location.state hoặc sessionStorage
  const [challengeId, setChallengeId] = useState<string>(
    () => locationState.challengeId || sessionStorage.getItem('ugtm_recovery_challenge_id') || '',
  );
  const email = locationState.email || sessionStorage.getItem('ugtm_recovery_email') || '';
  const [expiresAt, setExpiresAt] = useState<string>(
    () => locationState.expiresAt || sessionStorage.getItem('ugtm_recovery_expires_at') || '',
  );

  const [digits, setDigits] = useState<string[]>(Array<string>(OTP_LENGTH).fill(''));
  const [now, setNow] = useState(() => Date.now());
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [attempts, setAttempts] = useState(0);
  const [resendCooldownUntil, setResendCooldownUntil] = useState(() => Date.now() + 45 * 1000);

  const inputs = useRef<(HTMLInputElement | null)[]>([]);
  const mockChallenge = getChallenge();

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  // Nếu không có challenge thật lẫn mock challenge thì quay lại trang Quên mật khẩu
  if (!challengeId && !mockChallenge) {
    return <Navigate to="/forgot-password" replace />;
  }

  // Tính thời gian hết hạn OTP (mặc định 10 phút từ expiresAt)
  const expiryTimestamp = expiresAt
    ? new Date(expiresAt).getTime()
    : mockChallenge
      ? mockChallenge.expires
      : now + 10 * 60 * 1000;
  const seconds = Math.max(0, Math.ceil((expiryTimestamp - now) / 1000));
  const resendSeconds = Math.max(0, Math.ceil((resendCooldownUntil - now) / 1000));
  const isLocked = attempts >= MAX_OTP_ATTEMPTS;

  function handleDigitChange(value: string, index: number) {
    const numbers = value.replace(/\D/g, '').slice(0, OTP_LENGTH - index);
    setDigits((current) => {
      const next = [...current];
      if (!numbers) {
        next[index] = '';
      } else {
        numbers.split('').forEach((digit, offset) => {
          next[index + offset] = digit;
        });
      }
      return next;
    });
    if (numbers) {
      inputs.current[Math.min(index + numbers.length, OTP_LENGTH - 1)]?.focus();
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isLocked || isSubmitting || digits.some((d) => !d)) return;

    setError('');
    setNotice('');
    setIsSubmitting(true);

    const code = digits.join('');

    try {
      if (challengeId) {
        // Gọi API xác thực OTP thật: POST /api/identity/auth/recovery/verify
        const response = await verifyRecoveryOtpApi(challengeId, code);

        if (!response.success || !response.data) {
          throw new Error(response.error?.message || 'Mã xác thực không hợp lệ hoặc đã hết hạn.');
        }

        const { resetGrant } = response.data;

        // Lưu resetGrant để trang ResetPasswordPage sử dụng
        sessionStorage.setItem('ugtm_recovery_reset_grant', resetGrant);
        sessionStorage.removeItem('ugtm_recovery_challenge_id');

        navigate('/reset-password', {
          state: {
            resetGrant,
            email,
          },
        });
      } else {
        // Fallback mock nếu đang chạy dữ liệu mẫu
        const purpose = verifyOtp(code);
        if (purpose === 'reset') {
          navigate('/reset-password');
        } else {
          navigate('/login', {
            state: {
              success: 'Xác thực tài khoản thành công. Bạn có thể đăng nhập.',
            },
          });
        }
      }
    } catch (err: unknown) {
      setAttempts((prev) => prev + 1);
      if (axios.isAxiosError(err)) {
        const apiError = err.response?.data as ApiResponse<unknown> | undefined;
        setError(apiError?.error?.message || err.message || 'Mã xác thực không hợp lệ hoặc đã hết hạn.');
      } else {
        setError((err as Error).message || 'Đã có lỗi xảy ra khi xác thực OTP.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleResend() {
    if (resendSeconds > 0 || isLocked || isResending) return;

    setError('');
    setNotice('');
    setIsResending(true);

    try {
      if (email) {
        // Gọi API gửi lại OTP: POST /api/identity/auth/recovery/start
        const response = await startRecoveryApi(email);
        if (!response.success || !response.data) {
          throw new Error(response.error?.message || 'Không thể gửi lại mã OTP. Vui lòng thử lại sau.');
        }

        const newChallengeId = response.data.challengeId;
        const newExpiresAt = response.data.expiresAt;

        setChallengeId(newChallengeId);
        setExpiresAt(newExpiresAt);
        sessionStorage.setItem('ugtm_recovery_challenge_id', newChallengeId);
        sessionStorage.setItem('ugtm_recovery_expires_at', newExpiresAt);

        setDigits(Array<string>(OTP_LENGTH).fill(''));
        setResendCooldownUntil(Date.now() + 60 * 1000);
        setNotice('Đã gửi lại mã OTP mới đến hộp thư của bạn. Vui lòng kiểm tra email.');
        inputs.current[0]?.focus();
      } else {
        // Mock fallback
        resendChallenge();
        setDigits(Array<string>(OTP_LENGTH).fill(''));
        setResendCooldownUntil(Date.now() + 45 * 1000);
        setNotice('Đã gửi lại mã OTP demo thành công.');
      }
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const apiError = err.response?.data as ApiResponse<unknown> | undefined;
        setError(apiError?.error?.message || err.message || 'Không thể gửi lại mã OTP.');
      } else {
        setError((err as Error).message || 'Đã có lỗi xảy ra khi gửi lại mã OTP.');
      }
    } finally {
      setIsResending(false);
    }
  }

  const displayDestination = email || (mockChallenge?.method === 'email' ? 'Địa chỉ email trường' : 'SMS / Zalo');

  return (
    <section className="auth-card auth-otp">
      <Link className="auth-back" to="/login">
        <ArrowLeft size={14} />
        Quay lại Đăng nhập
      </Link>
      <div className="auth-shield">
        <Shield size={24} fill="currentColor" />
      </div>
      <h1>Xác thực Mã bảo mật OTP</h1>
      <p className="auth-subtitle">
        Hệ thống đã gửi mã OTP 6 chữ số đến hộp thư định danh trường của bạn
      </p>
      <div className="auth-destination">
        <Mail size={14} />
        <span>{displayDestination}</span>
        <Check size={14} className="auth-green" />
      </div>
      <form onSubmit={handleSubmit}>
        <div className="auth-otp-inputs">
          {digits.map((digit, index) => (
            <input
              key={index}
              ref={(node) => {
                inputs.current[index] = node;
              }}
              aria-label={`Chữ số OTP thứ ${index + 1}`}
              inputMode="numeric"
              autoComplete={index === 0 ? 'one-time-code' : 'off'}
              value={digit}
              disabled={isLocked || isSubmitting || seconds === 0}
              onChange={(event) => handleDigitChange(event.target.value, index)}
              onPaste={(event) => {
                event.preventDefault();
                handleDigitChange(event.clipboardData.getData('text'), index);
              }}
              onKeyDown={(event) => {
                if (event.key === 'Backspace' && !digit && index > 0)
                  inputs.current[index - 1]?.focus();
                if (event.key === 'ArrowLeft' && index > 0) inputs.current[index - 1]?.focus();
                if (event.key === 'ArrowRight' && index < OTP_LENGTH - 1)
                  inputs.current[index + 1]?.focus();
              }}
            />
          ))}
        </div>
        <p className="auth-countdown">
          <Clock size={14} />
          {seconds > 0 ? (
            <>
              Mã có hiệu lực trong{' '}
              <strong>
                {String(Math.floor(seconds / 60)).padStart(2, '0')}:
                {String(seconds % 60).padStart(2, '0')}
              </strong>
            </>
          ) : (
            <span className="text-red-600 font-medium">Mã đã hết hạn. Vui lòng bấm gửi lại mã bên dưới.</span>
          )}
        </p>
        <p className="auth-resend">
          Chưa nhận được mã?{' '}
          <button
            type="button"
            disabled={resendSeconds > 0 || isLocked || isResending}
            onClick={handleResend}
          >
            {isResending ? 'Đang gửi lại…' : `Gửi lại mã OTP ${resendSeconds > 0 ? `(${resendSeconds}s)` : ''}`}
          </button>
        </p>
        {error && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        {notice && (
          <p className="auth-success" role="status">
            {notice}
          </p>
        )}
        <Button
          type="submit"
          className="auth-submit"
          disabled={digits.some((digit) => !digit) || seconds === 0 || isLocked || isSubmitting}
          isLoading={isSubmitting}
          loadingText="Đang xác thực OTP…"
          rightIcon={<ArrowRight size={16} />}
        >
          Xác nhận mã OTP & Tiếp tục
        </Button>
      </form>
      <p className="auth-hint">
        <LockKeyhole size={14} />
        Bảo mật 2 lớp: OTP tối đa 5 lần thử. Sau 5 lần nhập sai, mã sẽ bị hủy.
      </p>
    </section>
  );
}
