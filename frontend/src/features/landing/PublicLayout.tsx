import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { ArrowRight, ArrowUpRight, Menu, X } from 'lucide-react';
import { useAuth } from '@/shared/auth';
import logo from '../../../assets/logo_UGTM.svg';
import '@fontsource-variable/manrope';
import './landing.css';

export function PublicLayout() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const [menuLocation, setMenuLocation] = useState<string | null>(null);
  const menuOpen = menuLocation === location.key;
  const setMenuOpen = (open: boolean) => setMenuLocation(open ? location.key : null);
  useEffect(() => {
    if (!location.hash) window.scrollTo({ top: 0, behavior: 'instant' });
    else
      requestAnimationFrame(() =>
        document.getElementById(location.hash.slice(1))?.scrollIntoView(),
      );
  }, [location.pathname, location.hash]);
  useEffect(() => {
    if (!menuOpen) return;
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuLocation(null);
        document.getElementById('public-menu-toggle')?.focus();
      }
    };
    document.addEventListener('keydown', escape);
    return () => document.removeEventListener('keydown', escape);
  }, [menuOpen]);
  return (
    <div className="public-site">
      <a href="#public-main" className="public-skip">
        Đến nội dung chính
      </a>
      <header className="public-header">
        <div className="public-container public-header-inner">
          <Link to="/welcome" className="public-brand" aria-label="UGTM — Trang chủ" onClick={() => setMenuLocation(null)}>
            <img src={logo} alt="UGTM" />
            <span>
              Hệ thống quản lý
              <br />
              khóa luận tốt nghiệp
            </span>
          </Link>
          <nav aria-label="Điều hướng chính" className="public-desktop-nav">
            <Link to="/welcome#campus">Khám phá UIT</Link>
            <Link
              to="/library"
              aria-current={location.pathname.startsWith('/library') ? 'page' : undefined}
            >
              Tra cứu khóa luận
            </Link>
            <Link to="/welcome#about">Về UGTM</Link>
          </nav>
          <div className="public-header-actions">
            <Link
              className="public-button public-button-blue public-login"
              to={isAuthenticated ? '/dashboard' : '/login'}
              onClick={() => setMenuLocation(null)}
            >
              {isAuthenticated ? 'Vào hệ thống' : 'Đăng nhập'}
              <ArrowUpRight size={17} />
            </Link>
            <button
              id="public-menu-toggle"
              type="button"
              className="public-icon-button public-menu-toggle"
              aria-label={menuOpen ? 'Đóng menu' : 'Mở menu'}
              aria-expanded={menuOpen}
              aria-controls="public-mobile-nav"
              onClick={() => setMenuOpen(!menuOpen)}
            >
              {menuOpen ? <X /> : <Menu />}
            </button>
          </div>
        </div>
        {menuOpen && (
          <nav id="public-mobile-nav" className="public-mobile-nav" aria-label="Điều hướng di động">
            <Link to="/welcome#campus">Khám phá UIT</Link>
            <Link to="/library">Tra cứu khóa luận</Link>
            <Link to="/welcome#about">Về UGTM</Link>
          </nav>
        )}
      </header>
      <main id="public-main" tabIndex={-1}>
        <Outlet />
      </main>
      <footer className="public-footer">
        <div className="public-container">
          <div className="public-footer-top">
            <Link to="/welcome" aria-label="UGTM — Trang chủ">
              <img src={logo} alt="UGTM" />
            </Link>
            <p>
              Kết nối ý tưởng.
              <br />
              Lưu giữ tri thức.
            </p>
            <Link to="/library">
              Khám phá kho khóa luận
              <ArrowRight size={19} />
            </Link>
          </div>
          <div className="public-footer-bottom">
            <span>UGTM · Hệ thống quản lý khóa luận tốt nghiệp</span>
            <a href="https://www.uit.edu.vn/" target="_blank" rel="noreferrer">
              Trường Đại học Công nghệ Thông tin
              <ArrowUpRight size={14} />
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}
