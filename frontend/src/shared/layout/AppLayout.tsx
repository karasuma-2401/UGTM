import { useState, useMemo, useEffect, useLayoutEffect, useRef } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import ugtmLogo from '../../../assets/logo_UGTM.svg';
import {
  Search,
  Menu,
  X,
  LogOut,
  ChevronRight,
  ArrowUpRight,
  CornerDownLeft,
  ChevronLeft,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
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
  const location = useLocation();
  const pageContent = useRef<HTMLElement>(null);
  const sidebar = useRef<HTMLElement>(null);
  const menuTrigger = useRef<HTMLButtonElement>(null);

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
          if (item.path === '/modules/identity') {
            return getIdentityAccess(hasPermission).canAccess;
          }
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
        setCommandOpen((prev) => !prev);
      } else if (event.key === 'Escape') {
        setCommandOpen(false);
      }
    }
    window.addEventListener('keydown', handleGlobalShortcut);
    return () => window.removeEventListener('keydown', handleGlobalShortcut);
  }, []);

  const flatNavItems = useMemo(
    () =>
      visibleSections.flatMap((section) =>
        section.items.map((item) => ({
          ...item,
          sectionTitle: section.title || 'Tổng quan',
        })),
      ),
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
    const exactOrPrefix = flatNavItems
      .filter((item) =>
        item.path === '/'
          ? location.pathname === '/'
          : location.pathname === item.path || location.pathname.startsWith(`${item.path}/`),
      )
      .sort((a, b) => b.path.length - a.path.length);
    return exactOrPrefix[0] ?? null;
  }, [flatNavItems, location.pathname]);

  const userInitial = user?.fullName ? user.fullName.trim().charAt(0).toUpperCase() : 'U';
  const primaryRoleLabel = useMemo(() => {
    if (!user?.roles || user.roles.length === 0) return user?.email || 'Thành viên';
    return user.roles.map((r) => ROLE_DISPLAY_LABELS[r] || r).join(', ');
  }, [user]);

  return (
    <div className="min-h-screen bg-[#f8fafc] flex text-slate-900">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          aria-hidden="true"
          className="fixed inset-0 z-40 bg-slate-950/40 backdrop-blur-xs lg:hidden"
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
          'fixed inset-y-0 left-0 z-50 flex w-72 max-w-[calc(100vw-2rem)] shrink-0 flex-col border-r border-slate-200 bg-slate-50 transition-[width,transform,visibility] duration-200 ease-out motion-reduce:transition-none lg:sticky lg:top-0 lg:h-screen lg:max-w-none lg:translate-x-0 lg:visible',
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
          className="absolute -right-3.5 top-16 z-10 hidden size-7 -translate-y-1/2 items-center justify-center rounded-full border border-slate-200/70 bg-white text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary lg:flex"
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
            'flex min-h-20 shrink-0 items-center justify-between gap-2 px-5 py-4',
            sidebarCollapsed && 'lg:min-h-16 lg:justify-center lg:px-2 lg:py-0',
          )}
        >
          <Link
            to="/"
            title="UGTM - Quản lý Khóa luận ĐH"
            className={cn(
              'flex min-w-0 flex-col items-start gap-1.5 rounded-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary',
              sidebarCollapsed && 'lg:items-center',
            )}
          >
            <img
              src={ugtmLogo}
              alt="UGTM"
              className={cn('h-5 w-auto shrink-0 object-contain', sidebarCollapsed && 'lg:h-3.5')}
            />
            <span className={cn('text-xs text-slate-600', sidebarCollapsed && 'lg:hidden')}>
              Quản lý Khóa luận ĐH
            </span>
          </Link>
          <button
            type="button"
            onClick={() => setSidebarOpen(false)}
            aria-label="Đóng menu điều hướng"
            className="flex size-11 shrink-0 items-center justify-center rounded-md text-slate-500 hover:bg-slate-100 hover:text-slate-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary lg:hidden"
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
            'shrink-0 border-t border-slate-200 px-4 py-3',
            sidebarCollapsed && 'lg:p-2',
          )}
        >
          <div
            className={cn('flex items-center gap-2.5', sidebarCollapsed && 'lg:flex-col lg:gap-1')}
          >
            <div
              title={sidebarCollapsed ? user?.fullName || 'Người dùng' : undefined}
              className="flex size-8 shrink-0 items-center justify-center rounded-full bg-slate-200 text-xs font-semibold text-slate-700"
            >
              {userInitial}
            </div>
            <div
              className={cn(
                'flex min-w-0 flex-1 items-center gap-2',
                sidebarCollapsed && 'lg:contents',
              )}
            >
              <div className={cn('min-w-0 flex-1', sidebarCollapsed && 'lg:hidden')}>
                <div className="truncate text-[length:var(--font-size-md)] font-medium text-slate-800">
                  {user?.fullName || 'Người dùng'}
                </div>
                <div className="truncate text-xs text-slate-600" title={primaryRoleLabel}>
                  {primaryRoleLabel}
                </div>
              </div>
              <button
                type="button"
                onClick={() => void logout()}
                disabled={isLoggingOut}
                title="Đăng xuất khỏi hệ thống"
                aria-label="Đăng xuất khỏi hệ thống"
                className="flex size-11 shrink-0 items-center justify-center rounded-md text-slate-500 transition-colors hover:bg-rose-50 hover:text-rose-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-50 lg:size-8"
              >
                <LogOut className="size-4" />
              </button>
            </div>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div inert={sidebarOpen ? true : undefined} className="flex-1 flex flex-col min-w-0">
        {/* Top Translucent Header */}
        <header className="h-16 bg-white/80 backdrop-blur-md border-b border-slate-200/75 px-4 lg:px-8 flex items-center justify-between gap-4 sticky top-0 z-30">
          <div className="flex items-center gap-3 min-w-0">
            <button
              ref={menuTrigger}
              type="button"
              onClick={() => setSidebarOpen(true)}
              aria-label="Mở menu điều hướng"
              aria-expanded={sidebarOpen}
              aria-controls="app-sidebar"
              className="lg:hidden p-2 rounded-lg text-slate-500 hover:bg-slate-100"
            >
              <Menu className="size-5" />
            </button>

            <nav aria-label="Vị trí trang" className="flex items-center gap-1.5 text-sm min-w-0">
              {activeNavEntry && (
                <>
                  {activeNavEntry.sectionTitle && (
                    <>
                      <span className="text-slate-500 hidden md:inline truncate">
                        {activeNavEntry.sectionTitle}
                      </span>
                      <ChevronRight className="size-4 text-slate-300 shrink-0 hidden md:inline" />
                    </>
                  )}
                  <span className="font-semibold text-slate-900 truncate">
                    {activeNavEntry.label}
                  </span>
                </>
              )}
            </nav>
          </div>

          {/* Header Right Actions: Quick Jump (Ctrl+K) & Mobile Logout */}
          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={() => {
                setCommandQuery('');
                setCommandOpen(true);
              }}
              className="flex items-center justify-between gap-3 h-9 px-3 sm:w-64 lg:w-72 rounded-xl border border-slate-200/90 bg-slate-50/70 hover:bg-white hover:border-slate-300 text-xs text-slate-500 shadow-2xs transition-all cursor-pointer"
              aria-label="Mở tìm kiếm nhanh phân hệ (Ctrl+K)"
            >
              <span className="flex items-center gap-2 truncate">
                <Search className="size-3.5 text-slate-400 shrink-0" />
                <span className="truncate">Đi tới phân hệ nhanh...</span>
              </span>
              <kbd className="hidden sm:inline-flex items-center gap-0.5 rounded-md border border-slate-200 bg-white px-1.5 py-0.5 font-mono text-[10px] font-medium text-slate-500">
                Ctrl K
              </kbd>
            </button>

            <Button
              variant="ghost"
              size="sm"
              onClick={() => void logout()}
              disabled={isLoggingOut}
              className="lg:hidden text-slate-500 hover:text-rose-600 hover:bg-rose-50 gap-1.5 text-xs font-medium"
              title="Đăng xuất"
            >
              <LogOut className="size-3.5" />
              <span className="hidden sm:inline">Đăng xuất</span>
            </Button>
          </div>
        </header>

        {/* Page Content Body */}
        <main ref={pageContent} className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          <Outlet />
        </main>
      </div>

      {/* Quick Command / Navigation Overlay (Ctrl+K) */}
      {commandOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Điều hướng nhanh"
          className="fixed inset-0 z-50 flex items-start justify-center bg-slate-950/45 pt-20 px-4 backdrop-blur-xs"
          onClick={() => setCommandOpen(false)}
        >
          <div
            className="w-full max-w-lg overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-[0_24px_64px_-12px_rgba(15,23,42,0.22)]"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
              <span className="text-sm font-semibold text-slate-900">Điều hướng nhanh</span>
              <button
                type="button"
                onClick={() => setCommandOpen(false)}
                aria-label="Đóng điều hướng nhanh"
                className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="p-3 border-b border-slate-100">
              <Input
                type="text"
                autoFocus
                value={commandQuery}
                onChange={(event) => setCommandQuery(event.target.value)}
                leftIcon={<Search className="size-4 text-slate-400" />}
                placeholder="Nhập tên trang hoặc phân hệ cần mở..."
              />
            </div>
            <div className="max-h-80 overflow-y-auto p-2 space-y-1">
              {filteredCommandItems.length === 0 ? (
                <p className="py-8 text-center text-sm text-slate-500">
                  Không tìm thấy phân hệ phù hợp với từ khóa.
                </p>
              ) : (
                filteredCommandItems.map((item) => {
                  const Icon = item.icon;
                  const isCurrent = location.pathname === item.path;
                  return (
                    <Link
                      key={item.path}
                      to={item.path}
                      onClick={() => setCommandOpen(false)}
                      className={cn(
                        'w-full flex items-center justify-between gap-3 px-3 py-2.5 rounded-xl text-left text-sm transition-colors group',
                        isCurrent
                          ? 'bg-blue-50/80 text-primary font-semibold'
                          : 'text-slate-700 hover:bg-slate-100/90 hover:text-slate-900',
                      )}
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <span
                          className={cn(
                            'flex size-8 shrink-0 items-center justify-center rounded-lg border transition-colors',
                            isCurrent
                              ? 'border-blue-200 bg-white text-primary'
                              : 'border-slate-200/80 bg-white text-slate-500 group-hover:text-slate-800',
                          )}
                        >
                          <Icon className="size-4" />
                        </span>
                        <div className="min-w-0">
                          <div className="truncate font-medium">{item.label}</div>
                          <div className="text-xs text-slate-400 truncate">{item.sectionTitle}</div>
                        </div>
                      </div>
                      <span className="flex items-center gap-1 text-xs text-slate-400 group-hover:text-primary shrink-0">
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
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
