import { CircleCheck, Mail, Shield, Smartphone } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AuthErrorMessage } from '../components/AuthErrorMessage';
import { AuthSubmitButton } from '../components/AuthSubmitButton';
import { BackToLoginLink } from '../components/BackToLoginLink';
import { DEMO_PROFILE } from '../mocks/authFixtures';
import { matchesProfile, startChallenge } from '../mocks/authMock';
import type { OtpMethod } from '../types/auth.types';

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [identifier, setIdentifier] = useState('');
  const [method, setMethod] = useState<OtpMethod>('email');
  const [error, setError] = useState('');
  const hasProfile = matchesProfile(identifier);
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    if (!hasProfile) {
      setError(
        `Không tìm thấy hồ sơ mẫu. Dùng MSSV ${DEMO_PROFILE.id} hoặc ${DEMO_PROFILE.email}.`,
      );
      return;
    }
    startChallenge('reset', method);
    navigate('/verify-otp');
  }
  return (
    <section className="auth-card auth-forgot">
      <BackToLoginLink />
      <h1>Quên mật khẩu truy cập?</h1>
      <p className="auth-subtitle">
        Nhập Email trường hoặc MSSV để nhận mã OTP xác thực khôi phục mật khẩu tài khoản Khóa luận.
      </p>
      <form onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="recovery-id">MSSV HOẶC EMAIL ĐỊNH DANH TRƯỜNG (SIS)</label>
          <div className="auth-input-wrap">
            <Mail size={16} />
            <input
              id="recovery-id"
              required
              value={identifier}
              onChange={(event) => setIdentifier(event.target.value)}
            />
            {hasProfile && <CircleCheck size={16} className="auth-green" />}
          </div>
          {hasProfile && (
            <p className="auth-hint">
              <span className="auth-green">✓</span> Tìm thấy hồ sơ sinh viên:{' '}
              <strong>{DEMO_PROFILE.name}</strong> (CNTT • K66)
            </p>
          )}
        </div>
        <fieldset className="auth-methods">
          <legend>PHƯƠNG THỨC NHẬN MÃ XÁC THỰC OTP</legend>
          {(['email', 'sms'] as const).map((value) => (
            <label key={value} className={`auth-method ${method === value ? 'selected' : ''}`}>
              <span className="auth-method-icon">
                {value === 'email' ? <Mail size={20} /> : <Smartphone size={20} />}
              </span>
              <span>
                <strong>
                  {value === 'email' ? 'Gửi mã qua Email trường' : 'Gửi mã qua tin nhắn SMS / Zalo'}
                </strong>
                {value === 'email' && <small>Khuyên dùng</small>}
                <p>{value === 'email' ? DEMO_PROFILE.email : '0912 ••• 678'}</p>
              </span>
              <input
                type="radio"
                name="otp-method"
                value={value}
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
            bộ quản trị hệ thống. Mã chỉ có hiệu lực trong vòng 5 phút.
          </p>
        </div>
        <AuthErrorMessage message={error} />
        <AuthSubmitButton>Gửi mã xác thực & Tiếp tục</AuthSubmitButton>
      </form>
      <p className="auth-bottom">
        Bạn đã nhớ lại mật khẩu truy cập? <Link to="/login">Quay lại Đăng nhập ngay</Link>
      </p>
      <p className="auth-demo">Demo: dùng MSSV {DEMO_PROFILE.id}.</p>
    </section>
  );
}
