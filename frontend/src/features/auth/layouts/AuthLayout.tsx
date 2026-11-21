import { Link, Outlet } from 'react-router-dom';

import logo from '../../../../assets/logo_UGTM.svg';

import '../styles/auth.css';

export function AuthLayout() {
  return (
    <div className="auth-shell">
      <header className="auth-header">
        <Link to="/welcome" className="auth-brand">
          <span className="auth-logo">
            <img src={logo} alt="UGTM" className="dark:brightness-125 dark:saturate-110" />
          </span>
          <span>
            HỆ THỐNG QUẢN LÝ KHÓA LUẬN TỐT NGHIỆP<small>UGTM PORTAL</small>
          </span>
        </Link>
        <Link to="/library" className="auth-public">
          Tra cứu khóa luận <span aria-hidden="true">↗</span>
        </Link>
      </header>
      <main className="auth-main">
        <Outlet />
      </main>
      <footer className="auth-footer">
        Hệ thống chỉ dành cho Sinh viên, Giảng viên và Cán bộ có nhiệm vụ trong Đợt Khóa luận tốt
        nghiệp.
        <br />
        Mọi truy cập đều được ghi nhật ký bảo mật theo quy chế an toàn dữ liệu học thuật.
      </footer>
    </div>
  );
}
