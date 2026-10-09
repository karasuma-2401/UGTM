import { Check, Clock, LockKeyhole, Mail, Shield } from 'lucide-react';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { AuthErrorMessage } from '../components/AuthErrorMessage';
import { AuthSubmitButton } from '../components/AuthSubmitButton';
import { BackToLoginLink } from '../components/BackToLoginLink';
import { OTP_LENGTH, MAX_OTP_ATTEMPTS } from '../constants/auth.constants';
import { DEMO_OTP, DEMO_PROFILE } from '../mocks/authFixtures';
import { getChallenge, resendChallenge, startChallenge, verifyOtp } from '../mocks/authMock';

export function OtpPage() {
  const navigate = useNavigate();
  const [digits, setDigits] = useState(Array<string>(OTP_LENGTH).fill(''));
  const [now, setNow] = useState(() => Date.now());
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const inputs = useRef<(HTMLInputElement | null)[]>([]);
  const challenge = getChallenge();
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  if (!challenge) {
    return <Navigate to="/forgot-password" replace />;
  }
  const seconds = Math.max(0, Math.ceil((challenge.expires - now) / 1000));
  const resendSeconds = Math.max(0, Math.ceil((challenge.resendAt - now) / 1000));
  const isLocked = challenge.attempts >= MAX_OTP_ATTEMPTS;
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
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    try {
      const purpose = verifyOtp(digits.join(''));
      if (purpose === 'reset') {
        navigate('/reset-password');
      } else {
        navigate('/login', {
          state: {
            success:
              'Kích hoạt tài khoản demo thành công. Bạn có thể đăng nhập với mật khẩu vừa tạo.',
          },
        });
      }
    } catch (err) {
      setError((err as Error).message);
    }
  }
  function handleResend() {
    if (resendChallenge()) {
      setNow(Date.now());
      setDigits(Array<string>(OTP_LENGTH).fill(''));
      setError('');
      setNotice('Đã tạo lại mã OTP demo.');
      inputs.current[0]?.focus();
    }
  }

  function handleMethodChange() {
    if (!challenge) {
      return;
    }

    if (resendSeconds > 0) {
      setNotice(`Vui lòng chờ ${resendSeconds}s trước khi đổi phương thức.`);
      return;
    }

    startChallenge(
      challenge.purpose,
      challenge.method === 'email' ? 'sms' : 'email',
      challenge.pendingPassword,
    );
    setNow(Date.now());
    setDigits(Array<string>(OTP_LENGTH).fill(''));
    setError('');
    setNotice('Đã đổi phương thức nhận OTP demo.');
  }
  return (
    <section className="auth-card auth-otp">
      <BackToLoginLink />
      <div className="auth-shield">
        <Shield size={24} fill="currentColor" />
      </div>
      <h1>Xác thực Mã bảo mật OTP</h1>
      <p className="auth-subtitle">
        Hệ thống đã gửi mã OTP 6 chữ số đến{' '}
        {challenge.method === 'email' ? 'địa chỉ email' : 'SMS / Zalo'}
      </p>
      <div className="auth-destination">
        <Mail size={14} />
        {challenge.method === 'email' ? DEMO_PROFILE.email : '0912 ••• 678'}
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
              disabled={isLocked}
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
          {seconds ? (
            <>
              Mã có hiệu lực trong{' '}
              <strong>
                {String(Math.floor(seconds / 60)).padStart(2, '0')}:
                {String(seconds % 60).padStart(2, '0')}
              </strong>
            </>
          ) : (
            'Mã đã hết hạn'
          )}
        </p>
        <p className="auth-resend">
          Chưa nhận được mã?{' '}
          <button type="button" disabled={resendSeconds > 0 || isLocked} onClick={handleResend}>
            Gửi lại mã OTP {resendSeconds > 0 && `(${resendSeconds}s)`}
          </button>
        </p>
        <AuthErrorMessage message={error} />
        {notice && (
          <p className="auth-success" role="status">
            {notice}
          </p>
        )}
        <AuthSubmitButton disabled={digits.some((digit) => !digit) || seconds === 0 || isLocked}>
          Xác nhận mã OTP
        </AuthSubmitButton>
      </form>
      <p className="auth-hint">
        <LockKeyhole size={14} />
        Bảo mật 2 lớp: luồng demo tạm khóa sau 5 lần nhập sai.
      </p>
      <p className="auth-bottom">
        Đổi phương thức xác thực?{' '}
        <button type="button" disabled={isLocked} onClick={handleMethodChange}>
          {challenge.method === 'email' ? 'Nhận mã qua SMS / Zalo' : 'Nhận mã qua Email'}
        </button>
      </p>
      <p className="auth-demo">
        Mã OTP demo: <strong>{DEMO_OTP}</strong>.
      </p>
    </section>
  );
}
