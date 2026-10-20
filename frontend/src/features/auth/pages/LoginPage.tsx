import axios from 'axios';
import { ArrowRight, Clock, IdCard, Lock } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import sso from '../../../../assets/logo_sso.png';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { ApiResponse } from '@/shared/api/types';
import { useAuth } from '@/shared/auth';
import { PasswordField } from '../components/PasswordField';
import {
  DEFAULT_ACCOUNT_LOCK_SECONDS,
  DEFAULT_RATE_LIMIT_SECONDS,
  LOGIN_LOCK_REASON_KEY,
  LOGIN_LOCK_UNTIL_KEY,
  REMEMBERED_IDENTIFIER_KEY,
} from '../constants/auth.constants';
import { loginApi } from '../services/authApi';

function formatCountdown(totalSeconds: number): string {
  if (totalSeconds <= 0) return '00:00';
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) {
    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  }
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login, isAuthenticated, isLoading, sessionError, sessionNotice, retrySession } =
    useAuth();
  const [identifier, setIdentifier] = useState(
    () => localStorage.getItem(REMEMBERED_IDENTIFIER_KEY) || '',
  );
  const [password, setPassword] = useState('');
  const [shouldRememberIdentifier, setShouldRememberIdentifier] = useState(false);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [notice, setNotice] = useState('');
  const success = location.state?.success as string | undefined;

  // 429 Lockout states with local persistence
  const [lockUntil, setLockUntil] = useState<number | null>(() => {
    const saved = localStorage.getItem(LOGIN_LOCK_UNTIL_KEY);
    if (!saved) return null;
    const timestamp = parseInt(saved, 10);
    if (isNaN(timestamp) || timestamp <= Date.now()) {
      localStorage.removeItem(LOGIN_LOCK_UNTIL_KEY);
      localStorage.removeItem(LOGIN_LOCK_REASON_KEY);
      return null;
    }
    return timestamp;
  });

  const [lockReason, setLockReason] = useState<string>(() => {
    return localStorage.getItem(LOGIN_LOCK_REASON_KEY) || '';
  });

  const [remainingSeconds, setRemainingSeconds] = useState<number>(() => {
    const saved = localStorage.getItem(LOGIN_LOCK_UNTIL_KEY);
    if (!saved) return 0;
    const timestamp = parseInt(saved, 10);
    if (isNaN(timestamp) || timestamp <= Date.now()) return 0;
    return Math.ceil((timestamp - Date.now()) / 1000);
  });

  const isLocked = Boolean(lockUntil && remainingSeconds > 0);

  const redirectFrom = (location.state as { from?: { pathname?: string } })?.from?.pathname || '/';

  useEffect(() => {
    if (isAuthenticated) {
      navigate(redirectFrom, { replace: true });
    }
  }, [isAuthenticated, navigate, redirectFrom]);

  useEffect(() => {
    if (!lockUntil) return;

    const tick = () => {
      const diff = Math.ceil((lockUntil - Date.now()) / 1000);
      if (diff <= 0) {
        setLockUntil(null);
        setRemainingSeconds(0);
        setLockReason('');
        localStorage.removeItem(LOGIN_LOCK_UNTIL_KEY);
        localStorage.removeItem(LOGIN_LOCK_REASON_KEY);
        setNotice('Thời gian tạm khóa đã kết thúc. Bạn có thể tiếp tục đăng nhập.');
      } else {
        setRemainingSeconds(diff);
      }
    };

    tick();
    const timer = setInterval(tick, 1000);
    return () => clearInterval(timer);
  }, [lockUntil]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isLocked || isSubmitting || isLoading || sessionError) return;

    setError('');
    setIsSubmitting(true);
    try {
      const response = await loginApi({
        identifier: identifier.trim(),
        password,
        rememberMe: shouldRememberIdentifier,
      });

      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Đăng nhập không thành công.');
      }

      localStorage.removeItem(LOGIN_LOCK_UNTIL_KEY);
      localStorage.removeItem(LOGIN_LOCK_REASON_KEY);

      if (shouldRememberIdentifier) {
        localStorage.setItem(REMEMBERED_IDENTIFIER_KEY, identifier.trim());
      } else {
        localStorage.removeItem(REMEMBERED_IDENTIFIER_KEY);
      }

      const { accessToken, user } = response.data;
      login(accessToken, user);

      navigate(redirectFrom, { replace: true });
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const status = err.response?.status;
        const apiError = err.response?.data as ApiResponse<unknown> | undefined;
        const errorCode = apiError?.error?.code;
        const errorMessage = apiError?.error?.message;

        // Xử lý response mã 429 hoặc các lỗi lockout từ backend Identity
        if (
          status === 429 ||
          errorCode === 'Identity.AccountLocked' ||
          errorCode === 'Identity.RateLimitExceeded'
        ) {
          // Lấy thời gian chờ từ header Retry-After nếu có
          const retryHeader = err.response?.headers?.['retry-after'];
          let lockSeconds = 0;
          if (retryHeader) {
            const parsed = parseInt(String(retryHeader), 10);
            if (!isNaN(parsed) && parsed > 0) {
              lockSeconds = parsed;
            }
          }

          // Nếu không có header Retry-After, dùng mặc định theo API contract
          if (!lockSeconds) {
            lockSeconds =
              errorCode === 'Identity.AccountLocked'
                ? DEFAULT_ACCOUNT_LOCK_SECONDS
                : DEFAULT_RATE_LIMIT_SECONDS;
          }

          const lockUntilTimestamp = Date.now() + lockSeconds * 1000;
          const reason =
            errorMessage ||
            (errorCode === 'Identity.AccountLocked'
              ? 'Tài khoản đang bị khóa tạm thời do nhập sai mật khẩu 5 lần liên tiếp. Vui lòng thử lại sau.'
              : 'Có quá nhiều yêu cầu đăng nhập từ thiết bị của bạn. Vui lòng thử lại sau.');

          localStorage.setItem(LOGIN_LOCK_UNTIL_KEY, lockUntilTimestamp.toString());
          localStorage.setItem(LOGIN_LOCK_REASON_KEY, reason);

          setLockUntil(lockUntilTimestamp);
          setLockReason(reason);
          setRemainingSeconds(lockSeconds);
          setError('');
          return;
        }

        setError(errorMessage || err.message || 'Đăng nhập không thành công.');
      } else {
        setError((err as Error).message || 'Đã có lỗi xảy ra khi đăng nhập.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="auth-card auth-login">
      <h1>Chào mừng trở lại!</h1>
      <p className="auth-subtitle">
        Đăng nhập tài khoản định danh để truy cập không gian làm việc Khóa luận
      </p>
      {sessionNotice && (
        <p className="auth-hint" role="status">
          {sessionNotice}
        </p>
      )}
      {isLoading && (
        <p className="auth-hint" role="status">
          Đang kiểm tra phiên đăng nhập…
        </p>
      )}
      {sessionError && (
        <div className="auth-error" role="alert">
          <p>{sessionError}</p>
          <Button type="button" variant="outline" onClick={retrySession}>
            Thử lại
          </Button>
        </div>
      )}
      {success && (
        <p className="auth-success" role="status">
          {success}
        </p>
      )}

      {isLocked && (
        <div
          role="alert"
          aria-live="assertive"
          className="my-3 p-3.5 rounded-lg border border-red-200 bg-red-50 text-red-900 shadow-sm transition-all"
        >
          <div className="flex items-start gap-3">
            <div className="p-2 bg-red-100 rounded-full text-red-600 shrink-0 mt-0.5">
              <Lock className="w-4 h-4" />
            </div>
            <div className="flex-1 min-w-0">
              <h4 className="text-xs font-semibold text-red-900 uppercase tracking-wide">
                Giao diện đăng nhập tạm thời bị khóa
              </h4>
              <p className="text-xs text-red-700 mt-1 leading-relaxed">
                {lockReason || 'Hệ thống đã tạm dừng nhận yêu cầu đăng nhập để đảm bảo an toàn.'}
              </p>
              <div className="mt-3 flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-red-200/80">
                <div className="flex items-center gap-1.5 text-xs text-red-800 font-medium">
                  <Clock className="w-3.5 h-3.5 text-red-600" />
                  <span>Tự động mở khóa sau:</span>
                  <span className="font-mono font-bold text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded border border-red-200">
                    {formatCountdown(remainingSeconds)}
                  </span>
                </div>
                <Link
                  to="/forgot-password"
                  className="text-xs font-semibold text-red-700 hover:text-red-900 underline underline-offset-2 hover:no-underline transition-colors ml-auto"
                >
                  Quên mật khẩu? Đặt lại ngay &rarr;
                </Link>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="auth-social">
        <button
          type="button"
          disabled={isSubmitting || isLocked || isLoading || Boolean(sessionError)}
          className="disabled:opacity-50 disabled:cursor-not-allowed"
          onClick={() =>
            setNotice('SSO chưa được kết nối. Vui lòng đăng nhập bằng MSSV hoặc email và mật khẩu.')
          }
        >
          <img src={sso} alt="" />
          Cổng SSO Trường
        </button>
      </div>

      {notice && !isLocked && (
        <p className="auth-hint" role="status">
          {notice}
        </p>
      )}

      <div className="auth-divider">HOẶC ĐĂNG NHẬP BẰNG MSSV / EMAIL CÁN BỘ</div>
      <form onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="identifier">MSSV HOẶC EMAIL CÁN BỘ</label>
          <Input
            id="identifier"
            autoComplete="username"
            required
            disabled={isSubmitting || isLocked || isLoading || Boolean(sessionError)}
            placeholder="Nhập MSSV hoặc Email..."
            leftIcon={<IdCard size={16} />}
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            className="bg-[#f1f3f6] border-[#eef0f4] text-[13px] h-[34px]"
          />
        </div>
        <PasswordField
          id="login-password"
          label="MẬT KHẨU TRUY CẬP"
          value={password}
          onChange={setPassword}
          autoComplete="current-password"
          disabled={isSubmitting || isLocked || isLoading || Boolean(sessionError)}
          className="bg-[#f1f3f6] border-[#eef0f4] text-[13px] h-[34px]"
        >
          <Link to="/forgot-password">Quên mật khẩu?</Link>
        </PasswordField>
        <label className={`auth-checkbox ${isLocked ? 'opacity-50 cursor-not-allowed' : ''}`}>
          <input
            type="checkbox"
            disabled={isSubmitting || isLocked || isLoading || Boolean(sessionError)}
            checked={shouldRememberIdentifier}
            onChange={(event) => setShouldRememberIdentifier(event.target.checked)}
          />
          Ghi nhớ MSSV/email trên thiết bị này
        </label>
        {error && !isLocked && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        <Button
          type="submit"
          className="auth-submit"
          disabled={isSubmitting || isLocked || isLoading || Boolean(sessionError)}
          isLoading={isSubmitting}
          loadingText="Đang đăng nhập…"
          rightIcon={isLocked ? <Lock size={16} /> : <ArrowRight size={16} />}
        >
          {isLocked
            ? `Tạm khóa đăng nhập (${formatCountdown(remainingSeconds)})`
            : 'Đăng nhập vào Không gian Khóa luận'}
        </Button>
      </form>
      <p className="auth-bottom">
        Sinh viên đủ điều kiện chưa kích hoạt tài khoản?{' '}
        <Link to="/activate">Kích hoạt tài khoản KLTN ngay</Link>
      </p>
    </section>
  );
}
