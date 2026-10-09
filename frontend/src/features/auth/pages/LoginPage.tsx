import { IdCard } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import google from '../../../../assets/logo_google.png';
import sso from '../../../../assets/logo_sso.png';
import { AuthErrorMessage } from '../components/AuthErrorMessage';
import { AuthSubmitButton } from '../components/AuthSubmitButton';
import { PasswordField } from '../components/PasswordField';
import { REMEMBERED_IDENTIFIER_KEY } from '../constants/auth.constants';
import { DEMO_PASSWORD, DEMO_PROFILE } from '../mocks/authFixtures';
import { loginMock } from '../mocks/authMock';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [identifier, setIdentifier] = useState(
    () => localStorage.getItem(REMEMBERED_IDENTIFIER_KEY) || '',
  );
  const [password, setPassword] = useState('');
  const [shouldRememberIdentifier, setShouldRememberIdentifier] = useState(false);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [notice, setNotice] = useState('');
  const success = location.state?.success as string | undefined;
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);
    try {
      await loginMock(identifier, password);
      if (shouldRememberIdentifier) {
        localStorage.setItem(REMEMBERED_IDENTIFIER_KEY, identifier.trim());
      } else {
        localStorage.removeItem(REMEMBERED_IDENTIFIER_KEY);
      }
      navigate('/');
    } catch (err) {
      setError((err as Error).message);
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
      {success && (
        <p className="auth-success" role="status">
          {success}
        </p>
      )}
      <div className="auth-social">
        <button
          type="button"
          onClick={() => setNotice('Cổng SSO đang ở chế độ demo. Hãy dùng tài khoản mẫu bên dưới.')}
        >
          <img src={sso} alt="" />
          Cổng SSO Trường
        </button>
        <button
          type="button"
          onClick={() => setNotice('Google đang ở chế độ demo. Hãy dùng tài khoản mẫu bên dưới.')}
        >
          <img src={google} alt="" />
          Google
        </button>
      </div>
      {notice && (
        <p className="auth-hint" role="status">
          {notice}
        </p>
      )}
      <div className="auth-divider">HOẶC ĐĂNG NHẬP BẰNG MSSV / EMAIL CÁN BỘ</div>
      <form onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="identifier">MSSV HOẶC EMAIL CÁN BỘ</label>
          <div className="auth-input-wrap">
            <IdCard size={16} />
            <input
              id="identifier"
              autoComplete="username"
              required
              value={identifier}
              onChange={(event) => setIdentifier(event.target.value)}
            />
          </div>
        </div>
        <PasswordField
          id="login-password"
          label="MẬT KHẨU TRUY CẬP"
          value={password}
          onChange={setPassword}
          autoComplete="current-password"
        >
          <Link to="/forgot-password">Quên mật khẩu?</Link>
        </PasswordField>
        <label className="auth-checkbox">
          <input
            type="checkbox"
            checked={shouldRememberIdentifier}
            onChange={(event) => setShouldRememberIdentifier(event.target.checked)}
          />
          Ghi nhớ MSSV/email trên thiết bị này
        </label>
        <AuthErrorMessage message={error} />
        <AuthSubmitButton disabled={isSubmitting}>
          {isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập vào Không gian Khóa luận'}
        </AuthSubmitButton>
      </form>
      <p className="auth-bottom">
        Sinh viên đủ điều kiện chưa kích hoạt tài khoản?{' '}
        <Link to="/activate">Kích hoạt tài khoản KLTN ngay</Link>
      </p>
      <details className="auth-demo">
        <summary>Tài khoản demo để test</summary>
        <p>
          MSSV: {DEMO_PROFILE.id}
          <br />
          Mật khẩu ban đầu: {DEMO_PASSWORD}
          <br />
          Mật khẩu đổi thử chỉ tồn tại đến khi tải lại trang.
        </p>
      </details>
    </section>
  );
}
