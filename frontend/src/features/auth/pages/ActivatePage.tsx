import { CircleCheck, IdCard, Mail, Phone } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AuthErrorMessage } from '../components/AuthErrorMessage';
import { AuthSubmitButton } from '../components/AuthSubmitButton';
import { PasswordFields } from '../components/PasswordFields';
import { DEMO_PROFILE } from '../mocks/authFixtures';
import { findEligibleStudentMock, startChallenge } from '../mocks/authMock';
import type { MockStudentProfile } from '../mocks/authMock.types';
import { validatePasswords } from '../utils/passwordValidation';

export function ActivatePage() {
  const navigate = useNavigate();
  const [studentId, setStudentId] = useState('');
  const [profile, setProfile] = useState<MockStudentProfile | null>(null);
  const [phone, setPhone] = useState('');
  const [lookupError, setLookupError] = useState('');
  const [password, setPassword] = useState('');
  const [confirmationPassword, setConfirmationPassword] = useState('');
  const [hasAgreedToTerms, setHasAgreedToTerms] = useState(false);
  const [error, setError] = useState('');
  function handleStudentIdChange(value: string) {
    setStudentId(value);
    setProfile(null);
    setPhone('');
    setPassword('');
    setConfirmationPassword('');
    setHasAgreedToTerms(false);
    setLookupError('');
    setError('');
  }

  function handleLookup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLookupError('');

    try {
      const studentProfile = findEligibleStudentMock(studentId);
      setProfile(studentProfile);
      setPhone(studentProfile.phone);
    } catch (error) {
      setProfile(null);
      setLookupError(error instanceof Error ? error.message : 'Không thể kiểm tra hồ sơ.');
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    try {
      if (!profile || profile.id !== studentId.trim()) {
        throw new Error('Vui lòng kiểm tra MSSV hợp lệ trước khi kích hoạt.');
      }
      validatePasswords(password, confirmationPassword);
      if (!/^0\d{9}$/.test(phone.replace(/\s/g, '')))
        throw new Error('Số điện thoại cần có 10 chữ số và bắt đầu bằng 0.');
      if (!hasAgreedToTerms) throw new Error('Vui lòng đồng ý với cam kết và quy chế.');
      startChallenge('activate', 'email', password);
      navigate('/verify-otp');
    } catch (err) {
      setError((err as Error).message);
    }
  }
  return (
    <section className="auth-card auth-activate">
      <p className="auth-top-link">
        Đã có tài khoản hoạt động? <Link to="/login">Đăng nhập ngay →</Link>
      </p>
      <h1>Kích hoạt Hồ sơ Khóa luận</h1>
      <div className="auth-divider">THÔNG TIN HỒ SƠ SINH VIÊN</div>
      {!profile && (
        <form onSubmit={handleLookup}>
          <div className="auth-field">
            <label htmlFor="student-id">Mã số sinh viên</label>
            <div className="auth-input-wrap">
              <IdCard size={16} />
              <input
                id="student-id"
                inputMode="numeric"
                autoComplete="off"
                placeholder="Nhập MSSV để kiểm tra hồ sơ"
                required
                value={studentId}
                onChange={(event) => handleStudentIdChange(event.target.value)}
                aria-invalid={Boolean(lookupError)}
                aria-describedby="student-lookup-hint"
              />
            </div>
            <p id="student-lookup-hint" className="auth-hint">
              Nhập MSSV và bấm kiểm tra để đối soát điều kiện tham gia khóa luận.
            </p>
          </div>
          <AuthErrorMessage message={lookupError} />
          <AuthSubmitButton disabled={!studentId.trim()}>Kiểm tra MSSV</AuthSubmitButton>
        </form>
      )}
      {profile && (
        <>
          <div className="auth-profile" role="status">
            <span className="auth-avatar">
              {profile.name
                .split(' ')
                .slice(-2)
                .map((part) => part[0])
                .join('')}
            </span>
            <div>
              <strong>{profile.name}</strong> <span>({profile.id})</span>
              <p>Khoa CNTT • K66 • Đủ ĐK Đợt 1 (GPA: 3.52 / 130 TC)</p>
            </div>
            <span className="auth-eligible">● Đủ điều kiện KLTN</span>
          </div>
          <form onSubmit={handleSubmit}>
            <div className="auth-two-col">
              <div className="auth-field">
                <div className="auth-label-row">
                  <label htmlFor="verified-student-id">Mã số sinh viên</label>
                  <button
                    type="button"
                    className="auth-change-student"
                    onClick={() => handleStudentIdChange(studentId)}
                  >
                    Đổi MSSV
                  </button>
                </div>
                <div className="auth-input-wrap">
                  <IdCard size={16} />
                  <input id="verified-student-id" value={profile.id} readOnly />
                  <CircleCheck size={16} className="auth-green" />
                </div>
              </div>
              <div className="auth-field">
                <label htmlFor="student-email">Email trường</label>
                <div className="auth-input-wrap">
                  <Mail size={16} />
                  <input id="student-email" value={profile.email} readOnly />
                </div>
              </div>
            </div>
            <div className="auth-field">
              <label htmlFor="phone">Số điện thoại</label>
              <div className="auth-input-wrap">
                <Phone size={16} />
                <input
                  id="phone"
                  type="tel"
                  autoComplete="tel"
                  required
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                />
              </div>
            </div>
            <div className="auth-divider">THIẾT LẬP MẬT KHẨU BẢO VỆ</div>
            <PasswordFields
              password={password}
              confirmationPassword={confirmationPassword}
              onPasswordChange={setPassword}
              onConfirmationChange={setConfirmationPassword}
            />
            <label className="auth-checkbox auth-terms">
              <input
                type="checkbox"
                checked={hasAgreedToTerms}
                onChange={(event) => setHasAgreedToTerms(event.target.checked)}
              />
              <span>
                Tôi cam kết chịu trách nhiệm về tính chính xác của hồ sơ và tuân thủ tuyệt đối{' '}
                <strong>Quy chế Khóa luận tốt nghiệp</strong> của Đại học Công nghệ Thông tin,
                ĐHQG-HCM.
              </span>
            </label>
            <AuthErrorMessage message={error} />
            <AuthSubmitButton>Kích hoạt Tài khoản & Nhận mã OTP</AuthSubmitButton>
          </form>
        </>
      )}
      <p className="auth-demo">Demo: nhập MSSV {DEMO_PROFILE.id} để kiểm tra hồ sơ hợp lệ.</p>
    </section>
  );
}
