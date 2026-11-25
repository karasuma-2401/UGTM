import { useState, useMemo, useEffect, useLayoutEffect, useRef } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import ugtmLogo from '../../../assets/logo_UGTM.svg';
import {
  Search,
  Menu,
  X,
  LogOut,
  ChevronRight,
  Check,
  ArrowUpRight,
  CornerDownLeft,
  ChevronLeft,
  Sun,
  Moon,
  UserRound,
  ShieldAlert,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/shared/auth';
import { NotificationBell } from '@/shared/notifications';
import { ThemeToggle, useTheme } from '@/shared/theme';
import { navigationSections, type NavItemConfig } from './navigationConfig';
import { CurrentAcademicBatch } from './CurrentAcademicBatch';
import { SidebarNavigation } from './SidebarNavigation';

export type NavItem = NavItemConfig;

const ROLE_DISPLAY_LABELS: Record<string, string> = {
  Admin: 'Quản trị viên',
  AcademicAffairs: 'Giáo vụ',
  Faculty: 'Ban chủ nhiệm Khoa',
  Lecturer: 'Giảng viên',
  CouncilMember: 'Thành viên Hội đồng',
  Student: 'Sinh viên',
};

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    try {
      return localStorage.getItem('ugtm:sidebar_collapsed') === 'true';
    } catch {
      return false;
    }
  });
  const [commandOpen, setCommandOpen] = useState(false);
  const [commandQuery, setCommandQuery] = useState('');
  const [openMenuPath, setOpenMenuPath] = useState<string | null>(null);
  const location = useLocation();
  const pageContent = useRef<HTMLElement>(null);
  const sidebar = useRef<HTMLElement>(null);
  const menuTrigger = useRef<HTMLButtonElement>(null);
  const sectionMenuRef = useRef<HTMLDivElement>(null);
  const sectionMenuTriggerRef = useRef<HTMLButtonElement>(null);
  const sectionMenuOpen = openMenuPath === location.pathname;

  useEffect(() => {
    if (!sidebarOpen) return;
    const desktop = window.matchMedia('(min-width: 1024px)');
    const closeOnDesktop = () => {
      if (desktop.matches) setSidebarOpen(false);
    };
    closeOnDesktop();
    desktop.addEventListener('change', closeOnDesktop);
    const element = sidebar.current;
    const trigger = menuTrigger.current;
    element?.querySelector<HTMLButtonElement>('[aria-label="Đóng menu điều hướng"]')?.focus();
    const trapFocus = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        setSidebarOpen(false);
      }
      if (event.key !== 'Tab' || !element) return;
      const controls = Array.from(
        element.querySelectorAll<HTMLElement>('a[href], button:not([disabled])'),
      ).filter((control) => control.getClientRects().length > 0);
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    };
    document.addEventListener('keydown', trapFocus);
    return () => {
      desktop.removeEventListener('change', closeOnDesktop);
      document.removeEventListener('keydown', trapFocus);
      trigger?.focus();
    };
  }, [sidebarOpen]);
  useLayoutEffect(() => {
    const element = pageContent.current;
    if (!element) return;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const animation = element.animate(
      reduced
        ? [{ opacity: 0.97 }, { opacity: 1 }]
        : [
            { opacity: 0.94, transform: 'translateY(10px)' },
            { opacity: 1, transform: 'translateY(0)' },
          ],
      { duration: reduced ? 120 : 360, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' },
    );
    return () => animation.cancel();
  }, [location.pathname]);
  const { user, logout, isLoggingOut, hasPermission, hasRole } = useAuth();
  const { theme, setTheme } = useTheme();

  const toggleSidebarCollapse = () => {
    setSidebarCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem('ugtm:sidebar_collapsed', String(next));
      } catch {
        // ignore storage errors
      }
      return next;
    });
  };

  const visibleSections = useMemo(() => {
    return navigationSections
      .map((section) => {
        if (section.roles && !hasRole(section.roles)) return null;
        if (section.permissions && !hasPermission(section.permissions)) return null;

        const visibleItems = section.items.filter((item) => {
          const requiredPerms = item.permissions || item.permission;
          if (requiredPerms) {
            const perms = Array.isArray(requiredPerms) ? requiredPerms : [requiredPerms];
            if (perms.length > 0) {
              const hasPerm = item.requireAllPermissions
                ? perms.every((p) => hasPermission(p))
                : hasPermission(perms);
              if (!hasPerm) return false;
            }
          }

          const requiredRoles = item.roles || item.role;
          if (requiredRoles) {
            const roles = Array.isArray(requiredRoles) ? requiredRoles : [requiredRoles];
            if (roles.length > 0 && !hasRole(roles)) {
              return false;
            }
          }

          return true;
        });

        if (visibleItems.length === 0) return null;
        return { ...section, items: visibleItems };
      })
      .filter((s): s is NonNullable<typeof s> => s !== null);
  }, [hasPermission, hasRole]);

  useEffect(() => {
    function handleGlobalShortcut(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setOpenMenuPath(null);
        setCommandOpen((prev) => !prev);
      } else if (event.key === 'Escape') {
        setCommandOpen(false);
        setOpenMenuPath((prev) => {
          if (prev !== null && sectionMenuRef.current?.contains(document.activeElement)) {
            requestAnimationFrame(() => sectionMenuTriggerRef.current?.focus());
          }
          return null;
        });
      }
    }
    window.addEventListener('keydown', handleGlobalShortcut);
    return () => window.removeEventListener('keydown', handleGlobalShortcut);
  }, []);

  useEffect(() => {
    if (!sectionMenuOpen) return;
    function handlePointerDown(event: MouseEvent) {
      if (sectionMenuRef.current && !sectionMenuRef.current.contains(event.target as Node)) {
        setOpenMenuPath(null);
      }
    }
    document.addEventListener('mousedown', handlePointerDown);
    return () => document.removeEventListener('mousedown', handlePointerDown);
  }, [sectionMenuOpen]);

  const flatNavItems = useMemo<
    Array<NavItemConfig & { sectionId: string; sectionTitle: string }>
  >(
    () => [
      ...visibleSections.flatMap((section) =>
        section.items.map((item) => ({
          ...item,
          sectionId: section.id,
          sectionTitle: section.title || 'Tổng quan',
        })),
      ),
      {
        label: 'Hồ sơ cá nhân',
        path: '/profile',
        icon: UserRound,
        sectionId: 'account',
        sectionTitle: 'Tài khoản và Bảo mật',
      },
    ],
    [visibleSections],
  );

  const filteredCommandItems = useMemo(() => {
    const q = commandQuery.trim().toLowerCase();
    if (!q) return flatNavItems;
    return flatNavItems.filter(
      (item) =>
        item.label.toLowerCase().includes(q) ||
        item.sectionTitle.toLowerCase().includes(q) ||
        item.path.toLowerCase().includes(q),
    );
  }, [flatNavItems, commandQuery]);

  const activeNavEntry = useMemo(() => {
    let bestEntry: (typeof flatNavItems)[number] | null = null;
    let bestLength = -1;
    for (const item of flatNavItems) {
      const candidates = item.activePaths ? [item.path, ...item.activePaths] : [item.path];
      for (const candidate of candidates) {
        const matched =
          candidate === '/'
            ? location.pathname === '/'
            : location.pathname === candidate || location.pathname.startsWith(`${candidate}/`);
        if (matched && candidate.length > bestLength) {
          bestLength = candidate.length;
          bestEntry = item;
        }
      }
    }
    return bestEntry;
  }, [flatNavItems, location.pathname]);

  const activeSectionItems = useMemo(() => {
    if (!activeNavEntry) return [];
    return (
      visibleSections.find((section) => section.id === activeNavEntry.sectionId)?.items ?? []
    );
  }, [visibleSections, activeNavEntry]);

  const primaryRoleLabel = useMemo(() => {
    if (!user?.roles || user.roles.length === 0) return user?.email || 'Thành viên';
    return user.roles.map((r) => ROLE_DISPLAY_LABELS[r] || r).join(', ');
  }, [user]);
  const isProfilePage = location.pathname === '/profile';

  return (
    <div className="min-h-screen bg-surface-canvas flex text-ink-primary">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          aria-hidden="true"
          className="fixed inset-0 z-40 bg-slate-950/40 dark:bg-black/60 backdrop-blur-xs lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        id="app-sidebar"
        ref={sidebar}
        aria-label="Menu điều hướng"
        role={sidebarOpen ? 'dialog' : undefined}
        aria-modal={sidebarOpen ? true : undefined}
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-72 max-w-[calc(100vw-2rem)] shrink-0 flex-col border-r border-line bg-surface-subtle dark:bg-[#0c1220] transition-[width,transform,visibility] duration-200 ease-out motion-reduce:transition-none lg:sticky lg:top-0 lg:h-screen lg:max-w-none lg:translate-x-0 lg:visible print:hidden',
          sidebarOpen ? 'visible translate-x-0' : 'invisible -translate-x-full',
          sidebarCollapsed ? 'lg:w-[72px]' : 'lg:w-72',
        )}
      >
        <button
          type="button"
          onClick={toggleSidebarCollapse}
          aria-label={sidebarCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          aria-expanded={!sidebarCollapsed}
          aria-controls="app-sidebar"
          title={sidebarCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          className="absolute -right-3.5 top-16 z-10 hidden size-7 -translate-y-1/2 items-center justify-center rounded-full border border-line-input/70 bg-surface-card text-ink-muted transition-colors hover:bg-surface-muted hover:text-ink-heading focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary lg:flex"
        >
          {sidebarCollapsed ? (
            <ChevronRight className="size-4 stroke-[1.75]" />
          ) : (
            <ChevronLeft className="size-4 stroke-[1.75]" />
          )}
        </button>
        {/* Sidebar Header / Brand */}
        <div
          className={cn(
            'flex h-16 shrink-0 items-center justify-between gap-2 px-5',
            sidebarCollapsed && 'lg:justify-center lg:px-2',
          )}
        >
          <Link
            to="/"
            title="UGTM - Quản lý Khóa luận UIT"
            className={cn(
              'flex min-w-0 flex-col items-start gap-1 rounded-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary',
              sidebarCollapsed && 'lg:items-center',
            )}
          >
            <img
              src={ugtmLogo}
              alt="UGTM"
              className={cn(
                'h-[18px] w-auto shrink-0 object-contain dark:brightness-125 dark:saturate-110',
                sidebarCollapsed && 'lg:h-3.5',
              )}
            />
            <span
              className={cn(
                'text-[11px] font-medium leading-none tracking-wide text-ink-muted',
                sidebarCollapsed && 'lg:hidden',
              )}
            >
              Quản lý Khóa luận UIT
            </span>
          </Link>
          <button
            type="button"
            onClick={() => setSidebarOpen(false)}
            aria-label="Đóng menu điều hướng"
            className="flex size-11 shrink-0 items-center justify-center rounded-md text-ink-muted hover:bg-surface-muted hover:text-ink-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary lg:hidden"
          >
            <X className="size-5" />
          </button>
        </div>

        <div className={cn('shrink-0', sidebarCollapsed && 'lg:hidden')}>
          <CurrentAcademicBatch />
        </div>

        {/* Navigation sections & items */}
        <SidebarNavigation
          key={user?.id ?? 'anonymous'}
          sections={visibleSections}
          pathname={location.pathname}
          activePath={activeNavEntry?.path}
          collapsed={sidebarCollapsed}
          collapsible={hasRole('Admin')}
          userId={user?.id}
          onNavigate={() => setSidebarOpen(false)}
        />

        {/* Sidebar Footer User profile */}
        <div
          className={cn(
            'shrink-0 border-t border-line px-3 py-2.5',
            sidebarCollapsed && 'lg:p-2',
          )}
        >
          <div
            className={cn('flex items-center gap-2', sidebarCollapsed && 'lg:flex-col lg:gap-1.5')}
          >
            <Link
              to="/profile"
              onClick={() => setSidebarOpen(false)}
              aria-current={isProfilePage ? 'page' : undefined}
              title={
                sidebarCollapsed
                  ? `${user?.fullName || 'Người dùng'} · Hồ sơ cá nhân`
                  : 'Mở hồ sơ cá nhân'
              }
              className={cn(
                'group flex min-w-0 flex-1 items-center gap-2.5 rounded-xl p-1.5 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary',
                sidebarCollapsed && 'lg:flex-none lg:justify-center',
                isProfilePage
                  ? 'bg-primary/10 text-primary dark:bg-primary/15'
                  : 'hover:bg-surface-muted/80',
              )}
            >
              <div className="relative shrink-0">
                <Avatar
                  name={user?.fullName || 'Người dùng'}
                  className="size-8 text-[11px] ring-0"
                />
                {user?.mustChangePassword && (
                  <span
                    title="Cần đổi mật khẩu"
                    className="absolute -right-0.5 -top-0.5 flex size-2.5 rounded-full bg-amber-500 ring-2 ring-surface-subtle"
                  />
                )}
              </div>
              <div className={cn('min-w-0 flex-1', sidebarCollapsed && 'lg:hidden')}>
                <div
                  className={cn(
                    'truncate text-[length:var(--font-size-md)] font-medium',
                    isProfilePage ? 'text-primary font-semibold' : 'text-ink-heading group-hover:text-ink-primary',
                  )}
                >
                  {user?.fullName || 'Người dùng'}
                </div>
                <div className="truncate text-xs text-ink-body" title={primaryRoleLabel}>
                  {primaryRoleLabel}
                </div>
              </div>
            </Link>
            <button
              type="button"
              onClick={() => void logout()}
              disabled={isLoggingOut}
              title="Đăng xuất khỏi hệ thống"
              aria-label="Đăng xuất khỏi hệ thống"
              className="flex size-11 shrink-0 items-center justify-center rounded-md text-ink-muted transition-colors hover:bg-tone-danger-bg hover:text-tone-danger-icon focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-50 lg:size-8"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div inert={sidebarOpen ? true : undefined} className="flex-1 flex flex-col min-w-0">
        {/* Top Translucent Header */}
        <header className="h-16 bg-surface-card/80 backdrop-blur-md border-b border-line/75 px-4 lg:px-8 flex items-center justify-between gap-4 sticky top-0 z-30 print:hidden">
          <div className="flex items-center gap-3 min-w-0">
            <button
              ref={menuTrigger}
              type="button"
              onClick={() => setSidebarOpen(true)}
              aria-label="Mở menu điều hướng"
              aria-expanded={sidebarOpen}
              aria-controls="app-sidebar"
              className="lg:hidden p-2 rounded-lg text-ink-muted hover:bg-surface-muted"
            >
              <Menu className="size-5" />
            </button>

            <nav aria-label="Vị trí trang" className="flex items-center gap-1.5 text-sm min-w-0">
              {activeNavEntry && (
                <>
                  {activeNavEntry.sectionTitle && (
                    <>
                      <div ref={sectionMenuRef} className="relative hidden md:block min-w-0">
                        <button
                          ref={sectionMenuTriggerRef}
                          type="button"
                          onClick={() =>
                            setOpenMenuPath((prev) =>
                              prev === location.pathname ? null : location.pathname,
                            )
                          }
                          aria-expanded={sectionMenuOpen}
                          aria-haspopup="menu"
                          aria-controls={sectionMenuOpen ? 'header-section-menu' : undefined}
                          className={cn(
                            'inline-flex max-w-full items-center gap-1 rounded-lg px-2 py-1 text-sm font-medium transition-colors select-none cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
                            sectionMenuOpen
                              ? 'bg-surface-muted text-ink-primary'
                              : 'text-ink-muted hover:bg-surface-muted/80 hover:text-ink-primary',
                          )}
                        >
                          <span className="truncate">{activeNavEntry.sectionTitle}</span>
                        </button>
                        {sectionMenuOpen && activeSectionItems.length > 0 && (
                          <div
                            id="header-section-menu"
                            role="menu"
                            aria-label={activeNavEntry.sectionTitle}
                            className="absolute left-0 top-full z-50 mt-1.5 min-w-64 w-max max-w-[min(22rem,calc(100vw-2rem))] rounded-xl border border-line-input/90 bg-surface-card p-1.5 shadow-lg animate-in fade-in-0 zoom-in-95 duration-150 dark:shadow-[0_16px_40px_rgba(0,0,0,0.55)]"
                          >
                            {activeSectionItems.map((item) => {
                              const Icon = item.icon;
                              const isCurrent = item.path === activeNavEntry.path;
                              return (
                                <Link
                                  key={item.path}
                                  to={item.path}
                                  role="menuitem"
                                  aria-current={isCurrent ? 'page' : undefined}
                                  onClick={() => setOpenMenuPath(null)}
                                  className={cn(
                                    'flex items-center justify-between gap-2.5 rounded-lg px-3 py-2 text-[length:var(--font-size-md)] transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary',
                                    isCurrent
                                      ? 'bg-primary/10 font-semibold text-primary dark:bg-primary/15'
                                      : 'font-normal text-ink-secondary hover:bg-surface-hover hover:text-ink-primary',
                                  )}
                                >
                                  <span className="flex min-w-0 items-center gap-2.5">
                                    <Icon
                                      aria-hidden="true"
                                      className={cn(
                                        'size-4 shrink-0 stroke-[1.75]',
                                        isCurrent ? 'text-primary' : 'text-ink-subtle',
                                      )}
                                    />
                                    <span className="truncate">{item.label}</span>
                                  </span>
                                  {isCurrent && (
                                    <Check
                                      aria-hidden="true"
                                      className="size-4 shrink-0 text-primary"
                                    />
                                  )}
                                </Link>
                              );
                            })}
                          </div>
                        )}
                      </div>
                      <ChevronRight className="size-4 text-ink-faint shrink-0 hidden md:inline" />
                    </>
                  )}
                  <Link
                    to={activeNavEntry.path}
                    className="min-w-0 truncate rounded-md px-1.5 py-1 -mx-1.5 font-semibold text-ink-primary transition-colors hover:bg-surface-muted hover:text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                  >
                    {activeNavEntry.label}
                  </Link>
                </>
              )}
              {!activeNavEntry && isProfilePage && (
                <>
                  <span className="hidden md:inline text-sm font-medium text-ink-muted">
                    Tài khoản và Bảo mật
                  </span>
                  <ChevronRight className="size-4 text-ink-faint shrink-0 hidden md:inline" />
                  <Link
                    to="/profile"
                    className="min-w-0 truncate rounded-md px-1.5 py-1 -mx-1.5 font-semibold text-ink-primary transition-colors hover:bg-surface-muted hover:text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                  >
                    Hồ sơ cá nhân
                  </Link>
                </>
              )}
            </nav>
          </div>

          {/* Header Right Actions: Quick Jump (Ctrl+K), Theme Switcher & Mobile Logout */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => {
                setOpenMenuPath(null);
                setCommandQuery('');
                setCommandOpen(true);
              }}
              className="flex items-center justify-between gap-3 h-9 px-3 sm:w-56 lg:w-64 rounded-xl border border-line-input/90 bg-surface-muted/70 hover:bg-surface-hover hover:border-line-strong text-xs text-ink-muted shadow-2xs transition-all cursor-pointer"
              aria-label="Mở tìm kiếm nhanh phân hệ (Ctrl+K)"
            >
              <span className="flex items-center gap-2 truncate">
                <Search className="size-3.5 text-ink-subtle shrink-0" />
                <span className="truncate">Đi tới phân hệ nhanh...</span>
              </span>
              <kbd className="hidden sm:inline-flex shrink-0 whitespace-nowrap items-center gap-0.5 rounded-md border border-line-input bg-surface-card px-1.5 py-0.5 font-mono text-[10px] font-medium text-ink-muted">
                Ctrl K
              </kbd>
            </button>

            <NotificationBell />

            <ThemeToggle />

            <Button
              variant="ghost"
              size="sm"
              onClick={() => void logout()}
              disabled={isLoggingOut}
              className="lg:hidden text-ink-muted hover:text-tone-danger-icon hover:bg-tone-danger-bg gap-1.5 text-xs font-medium"
              title="Đăng xuất"
            >
              <LogOut className="size-3.5" />
              <span className="hidden sm:inline">Đăng xuất</span>
            </Button>
          </div>
        </header>

        {/* Page Content Body */}
        <main
          ref={pageContent}
          className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto print:p-0 print:max-w-none"
        >
          {user?.mustChangePassword && !isProfilePage && (
            <InlineAlert
              tone="warning"
              className="mb-5 print:hidden"
              title="Tài khoản đang được yêu cầu đổi mật khẩu"
              action={
                <Button asChild size="sm">
                  <Link to="/profile?tab=security">
                    <ShieldAlert className="size-3.5" />
                    Đổi mật khẩu ngay
                  </Link>
                </Button>
              }
            >
              Mật khẩu hiện tại là mật khẩu tạm do Quản trị viên đặt lại. Vui lòng thiết lập mật khẩu mới để tiếp tục sử dụng đầy đủ các tính năng của hệ thống.
            </InlineAlert>
          )}
          <Outlet />
        </main>
      </div>

      {/* Quick Command / Navigation Overlay (Ctrl+K) */}
      {commandOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Điều hướng nhanh"
          className="fixed inset-0 z-50 flex items-start justify-center bg-slate-950/45 dark:bg-black/65 pt-20 px-4 backdrop-blur-xs"
          onClick={() => setCommandOpen(false)}
        >
          <div
            className="w-full max-w-lg overflow-hidden rounded-2xl border border-line-input/80 bg-surface-card shadow-[0_24px_64px_-12px_rgba(15,23,42,0.22)] dark:shadow-[0_24px_64px_-12px_rgba(0,0,0,0.65)]"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-line-subtle px-4 py-3">
              <span className="text-sm font-semibold text-ink-primary">Điều hướng nhanh</span>
              <button
                type="button"
                onClick={() => setCommandOpen(false)}
                aria-label="Đóng điều hướng nhanh"
                className="rounded-lg p-1 text-ink-subtle hover:bg-surface-muted hover:text-ink-secondary"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="p-3 border-b border-line-subtle">
              <Input
                type="text"
                autoFocus
                value={commandQuery}
                onChange={(event) => setCommandQuery(event.target.value)}
                leftIcon={<Search className="size-4 text-ink-subtle" />}
                placeholder="Nhập tên trang, phân hệ hoặc giao diện..."
              />
            </div>
            <div className="max-h-80 overflow-y-auto p-2 space-y-1">
              {filteredCommandItems.length === 0 ? (
                <p className="py-8 text-center text-sm text-ink-muted">
                  Không tìm thấy phân hệ phù hợp với từ khóa.
                </p>
              ) : (
                filteredCommandItems.map((item) => {
                  const Icon = item.icon;
                  const isCurrent = activeNavEntry?.path === item.path;
                  return (
                    <Link
                      key={item.path}
                      to={item.path}
                      onClick={() => setCommandOpen(false)}
                      className={cn(
                        'w-full flex items-center justify-between gap-3 px-3 py-2.5 rounded-xl text-left text-sm transition-colors group',
                        isCurrent
                          ? 'bg-tone-info-bg text-primary font-semibold'
                          : 'text-ink-secondary hover:bg-surface-muted/90 hover:text-ink-primary',
                      )}
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <span
                          className={cn(
                            'flex size-8 shrink-0 items-center justify-center rounded-lg border transition-colors',
                            isCurrent
                              ? 'border-tone-info-border bg-surface-card text-primary'
                              : 'border-line-input/80 bg-surface-elevated text-ink-muted group-hover:text-ink-heading',
                          )}
                        >
                          <Icon className="size-4" />
                        </span>
                        <div className="min-w-0">
                          <div className="truncate font-medium">{item.label}</div>
                          <div className="text-xs text-ink-subtle truncate">{item.sectionTitle}</div>
                        </div>
                      </div>
                      <span className="flex items-center gap-1 text-xs text-ink-subtle group-hover:text-primary shrink-0">
                        {isCurrent ? (
                          <span>Đang mở</span>
                        ) : (
                          <>
                            <CornerDownLeft className="size-3.5" />
                            <ArrowUpRight className="size-3.5" />
                          </>
                        )}
                      </span>
                    </Link>
                  );
                })
              )}

              {/* Quick Theme Actions */}
              <div className="mt-2 border-t border-line-subtle pt-2">
                <div className="px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-ink-subtle">
                  Giao diện hiển thị
                </div>
                <div className="grid grid-cols-2 gap-1.5 px-1 pt-1">
                  {(
                    [
                      { value: 'light', label: 'Sáng', Icon: Sun },
                      { value: 'dark', label: 'Tối', Icon: Moon },
                    ] as const
                  ).map(({ value, label, Icon }) => {
                    const active = theme === value;
                    return (
                      <button
                        key={value}
                        type="button"
                        aria-pressed={active}
                        onClick={(event) => {
                          setTheme(value, {
                            sourceElement: event.currentTarget,
                            onBeforeCommit: () => setCommandOpen(false),
                          });
                        }}
                        className={cn(
                          'flex items-center justify-center gap-1.5 rounded-lg border px-2.5 py-2 text-xs font-medium transition-colors cursor-pointer',
                          active
                            ? 'border-primary/40 bg-primary/10 text-primary dark:bg-primary/15'
                            : 'border-line-input/80 bg-surface-muted/70 text-ink-body hover:bg-surface-muted hover:text-ink-primary',
                        )}
                      >
                        <Icon className="size-3.5 shrink-0" aria-hidden="true" />
                        <span>{label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
