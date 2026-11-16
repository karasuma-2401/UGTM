import { Link, Outlet } from 'react-router-dom';
import { ThemeToggle } from '@/shared/theme';

import logo from '../../../../assets/logo_UGTM.svg';

import '../styles/auth.css';

export function AuthLayout() {
  return (
    <div className="auth-shell">
      <header className="auth-header">
        <Link to="/login" className="auth-brand">
          <span className="auth-logo">
            <img src={logo} alt="UGTM" className="dark:brightness-125 dark:saturate-110" />
          </span>
          <span>
            HỆ THỐNG QUẢN LÝ KHÓA LUẬN TỐT NGHIỆP<small>UGTM PORTAL</small>
          </span>
        </Link>
        <div className="auth-header-actions">
          <Link to="/modules/topic-catalog" className="auth-public">
            Tra cứu Đề tài công khai <span aria-hidden="true">↗</span>
          </Link>
          <ThemeToggle variant="auth" />
        </div>
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
