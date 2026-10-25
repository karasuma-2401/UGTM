import axios from 'axios';
import { ArrowRight, IdCard } from 'lucide-react';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { ApiResponse } from '@/shared/api/types';
import { PasswordFields } from '../components/PasswordFields';
import {
  completeActivationApi,
  resendActivationApi,
  validateActivationApi,
  type ActivationValidateData,
} from '../services/authApi';
import { validatePasswords } from '../utils/passwordValidation';

function activationError(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    if (error.response?.status === 429) {
      return 'Bạn đã gửi quá nhiều yêu cầu kích hoạt. Vui lòng thử lại sau ít phút.';
    }
    const errorCode = error.response?.data?.error?.code;
    if (errorCode === 'Identity.InvalidActivationToken') {
      return 'Link kích hoạt không hợp lệ, đã hết hạn hoặc đã được sử dụng. Hãy yêu cầu email mới bên dưới.';
    }
    if (errorCode === 'Identity.WeakPassword') {
      return 'Mật khẩu chưa đáp ứng tiêu chuẩn bảo mật (tối thiểu 12 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt).';
    }
    return (
      error.response?.data?.error?.message || 'Không thể kết nối đến máy chủ. Vui lòng thử lại.'
    );
  }
  return error instanceof Error ? error.message : 'Không thể xử lý yêu cầu kích hoạt.';
}

function extractToken(hash: string, search: string): string {
  const rawHash = hash.startsWith('#') ? hash.slice(1) : hash;
  const hashToken = new URLSearchParams(rawHash).get('token');
  if (hashToken) return hashToken.trim();

  const searchToken = new URLSearchParams(search).get('token');
  if (searchToken) return searchToken.trim();

  return '';
}

export function ActivatePage() {
  const location = useLocation();
  const token = extractToken(location.hash, location.search);
  return <ActivationForm key={token} token={token} />;
}

function ActivationForm({ token }: { token: string }) {
  const navigate = useNavigate();
  const [identifier, setIdentifier] = useState('');
  const [profile, setProfile] = useState<ActivationValidateData | null>(null);
  const [password, setPassword] = useState('');
  const [confirmationPassword, setConfirmationPassword] = useState('');
  const [checking, setChecking] = useState(Boolean(token));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [retry, setRetry] = useState(0);
  const [cooldownUntil, setCooldownUntil] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  const pendingValidation = useRef<{
    token: string;
    retry: number;
    promise: ReturnType<typeof validateActivationApi>;
  } | null>(null);

  useEffect(() => {
    let active = true;
    if (!token) return;

    if (pendingValidation.current?.token !== token || pendingValidation.current.retry !== retry) {
      pendingValidation.current = { token, retry, promise: validateActivationApi(token) };
    }
    void pendingValidation.current.promise
      .then((response) => {
        if (!active) return;
        if (!response.success || !response.data) {
          throw new Error(response.error?.message || 'Link kích hoạt không hợp lệ.');
        }
        setProfile(response.data);
      })
      .catch((err: unknown) => {
        if (active) setError(activationError(err));
      })
      .finally(() => {
        if (active) setChecking(false);
      });
    return () => {
      active = false;
    };
  }, [token, retry]);

  useEffect(() => {
    if (cooldownUntil <= Date.now()) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [cooldownUntil]);

  const cooldown = Math.max(0, Math.ceil((cooldownUntil - now) / 1000));

  async function handleResend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting || cooldown > 0 || !identifier.trim()) return;
    setIsSubmitting(true);
    setError('');
    setNotice('');
    try {
      const response = await resendActivationApi(identifier.trim());
      if (!response.success)
        throw new Error(response.error?.message || 'Không thể yêu cầu email kích hoạt.');
      setNotice(
        'Nếu tài khoản đang chờ kích hoạt, hệ thống sẽ gửi hướng dẫn đến email đã đăng ký. Hãy kiểm tra cả thư mục spam và sử dụng link trong email mới nhất.',
      );
      setNow(Date.now());
      setCooldownUntil(Date.now() + 60_000);
    } catch (err) {
      setError(activationError(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleComplete(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting || !profile || !token) return;
    setError('');
    setIsSubmitting(true);
    try {
      validatePasswords(password, confirmationPassword);
      const response = await completeActivationApi(token, password);
      if (!response.success)
        throw new Error(response.error?.message || 'Không thể kích hoạt tài khoản.');
      setPassword('');
      setConfirmationPassword('');
      navigate('/login', {
        replace: true,
        state: { success: 'Kích hoạt tài khoản thành công. Hãy đăng nhập bằng mật khẩu vừa tạo.' },
      });
    } catch (err) {
      if (
        axios.isAxiosError<ApiResponse<unknown>>(err) &&
        err.response?.data?.error?.code === 'Identity.InvalidActivationToken'
      ) {
        setProfile(null);
      }
      setError(activationError(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="auth-card auth-activate">
      <p className="auth-top-link">
        Đã có tài khoản? <Link to="/login">Đăng nhập ngay →</Link>
      </p>
      <h1>Kích hoạt tài khoản</h1>
      <p className="auth-subtitle">
        {profile
          ? 'Thiết lập mật khẩu để hoàn tất kích hoạt tài khoản.'
          : 'Nhận link kích hoạt qua email đã đăng ký với nhà trường.'}
      </p>
      {checking && (
        <p className="auth-hint" role="status">
          Đang kiểm tra link kích hoạt…
        </p>
      )}
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
      {!checking && profile && (
        <form onSubmit={handleComplete}>
          <div className="auth-profile">
            <div>
              <strong>{profile.fullName}</strong>
              <p>{profile.maskedEmail}</p>
            </div>
          </div>
          <PasswordFields
            password={password}
            confirmationPassword={confirmationPassword}
            onPasswordChange={setPassword}
            onConfirmationChange={setConfirmationPassword}
            disabled={isSubmitting}
            showRules
            autoValidateMismatch
          />
          <Button
            type="submit"
            className="auth-submit"
            isLoading={isSubmitting}
            loadingText="Đang kích hoạt…"
            rightIcon={<ArrowRight size={16} />}
          >
            Kích hoạt tài khoản
          </Button>
          <p className="auth-hint">
            Link không còn dùng được? <Link to="/activate">Yêu cầu email mới</Link>
          </p>
        </form>
      )}
      {!checking && !profile && (
        <>
          {token && (
            <Button
              type="button"
              variant="outline"
              disabled={isSubmitting}
              onClick={() => {
                setChecking(true);
                setError('');
                setNotice('');
                setRetry((value) => value + 1);
              }}
            >
              Kiểm tra lại link
            </Button>
          )}
          <form onSubmit={handleResend}>
            <div className="auth-field">
              <label htmlFor="activation-identifier">MSSV HOẶC EMAIL ĐÃ ĐĂNG KÝ</label>
              <Input
                id="activation-identifier"
                autoComplete="username"
                required
                value={identifier}
                onChange={(event) => setIdentifier(event.target.value)}
                disabled={isSubmitting}
                placeholder="Nhập MSSV hoặc email…"
                leftIcon={<IdCard size={16} />}
              />
            </div>
            <Button
              type="submit"
              className="auth-submit"
              isLoading={isSubmitting}
              disabled={cooldown > 0 || !identifier.trim()}
              loadingText="Đang gửi yêu cầu…"
              rightIcon={<ArrowRight size={16} />}
            >
              {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : 'Gửi email kích hoạt'}
            </Button>
            <p className="auth-hint">
              Tài khoản cần được nhà trường tạo trước. Yêu cầu này không đăng ký tài khoản mới.
            </p>
          </form>
        </>
      )}
    </section>
  );
}
