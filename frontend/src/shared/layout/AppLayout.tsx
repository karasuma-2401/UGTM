import { useState, useMemo, useEffect } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
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
  ChevronRight as ChevronRightIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
import { navigationSections, type NavItemConfig } from './navigationConfig';
import { CurrentAcademicBatch } from './CurrentAcademicBatch';

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
  const totalVisibleItems = visibleSections.reduce((acc, s) => acc + s.items.length, 0);

  return (
    <div className="min-h-screen bg-[#f8fafc] flex text-slate-900">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-slate-950/40 backdrop-blur-xs lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={cn(
          'relative fixed inset-y-0 left-0 z-50 bg-white border-r border-slate-200/80 flex flex-col transition-all duration-200 ease-out lg:static lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
          sidebarCollapsed ? 'lg:w-[72px]' : 'lg:w-64 w-64',
        )}
      >
        {/* Toggle Button docked on the sidebar border (Modern SaaS pill style) */}
        <button
          type="button"
          onClick={toggleSidebarCollapse}
          aria-label={sidebarCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          title={sidebarCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          className="hidden lg:flex absolute -right-3.5 top-5 z-20 size-7 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-sm hover:bg-slate-50 hover:text-slate-900 transition-colors cursor-pointer"
        >
          {sidebarCollapsed ? (
            <ChevronRightIcon className="size-3.5" />
          ) : (
            <ChevronLeft className="size-3.5" />
          )}
        </button>

        {/* Sidebar Header / Brand */}
        <div className={cn(
          'h-16 flex items-center border-b border-slate-100 transition-all duration-200',
          sidebarCollapsed ? 'justify-center px-2' : 'justify-between px-4',
        )}>
          <Link
            to="/"
            title="UGTM - Quản lý Khóa luận ĐH"
            className={cn(
              'flex items-center min-w-0 group',
              sidebarCollapsed ? 'justify-center' : 'gap-3',
            )}
          >
            <img src={ugtmLogo} alt="UGTM Logo" className="h-5 w-auto object-contain shrink-0" />
            {!sidebarCollapsed && (
              <>
                <div className="h-4 w-px bg-slate-200 shrink-0" aria-hidden="true" />
                <div className="min-w-0">
                  <div className="text-xs font-medium text-slate-500 truncate">
                    Quản lý Khóa luận ĐH
                  </div>
                </div>
              </>
            )}
          </Link>
          <button
            type="button"
            onClick={() => setSidebarOpen(false)}
            aria-label="Đóng menu điều hướng"
            className="lg:hidden p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100"
          >
            <X className="size-5" />
          </button>
        </div>

        {!sidebarCollapsed && <CurrentAcademicBatch />}

        {/* Navigation sections & items */}
        <nav
          aria-label="Điều hướng chính"
          className={cn(
            'flex-1 overflow-y-auto py-2 transition-all duration-200',
            sidebarCollapsed ? 'px-2 space-y-4' : 'px-3 space-y-5',
          )}
        >
          {totalVisibleItems === 0 ? (
            <div className="px-3 py-6 text-center text-xs text-slate-400 font-medium">
              {!sidebarCollapsed && 'Không có phân hệ nào khả dụng cho tài khoản này'}
            </div>
          ) : (
            visibleSections.map((section) => (
              <div key={section.id} className="space-y-1">
                {section.title && !sidebarCollapsed && (
                  <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider px-3 py-1">
                    {section.title}
                  </div>
                )}
                {section.items.map((item) => {
                  const Icon = item.icon;
                  const isActive =
                    item.path === '/'
                      ? location.pathname === item.path
                      : location.pathname === item.path ||
                        location.pathname.startsWith(`${item.path}/`);

                  return (
                    <NavLink
                      key={item.path}
                      to={item.path}
                      onClick={() => setSidebarOpen(false)}
                      title={sidebarCollapsed ? item.label : undefined}
                      style={isActive ? { background: 'var(--gradient-brand)' } : undefined}
                      className={cn(
                        'relative flex items-center rounded-xl text-sm transition-all duration-150 group',
                        sidebarCollapsed
                          ? 'justify-center p-2.5 my-1'
                          : 'justify-between px-3 py-2',
                        isActive
                          ? 'text-white shadow-sm font-semibold'
                          : 'font-medium text-slate-600 hover:bg-slate-100/80 hover:text-slate-900',
                      )}
                    >
                      <div className={cn(
                        'flex items-center min-w-0',
                        sidebarCollapsed ? 'justify-center' : 'gap-3',
                      )}>
                        <Icon
                          className={cn(
                            'size-4.5 shrink-0 transition-colors',
                            isActive
                              ? 'text-white'
                              : 'text-slate-400 group-hover:text-slate-700',
                          )}
                        />
                        {!sidebarCollapsed && <span className="truncate">{item.label}</span>}
                      </div>
                      {item.badge && !sidebarCollapsed && (
                        <Badge
                          variant={isActive ? 'outline' : 'secondary'}
                          className={cn(
                            'text-[10px] px-1.5 py-0 ml-2 shrink-0',
                            isActive && 'border-white/40 text-white bg-white/20',
                          )}
                        >
                          {item.badge}
                        </Badge>
                      )}
                    </NavLink>
                  );
                })}
              </div>
            ))
          )}
        </nav>

        {/* Sidebar Footer User profile */}
        <div className={cn(
          'p-3 border-t border-slate-100 bg-slate-50/40 transition-all duration-200',
          sidebarCollapsed && 'p-2',
        )}>
          <div className={cn(
            'flex items-center rounded-xl border border-slate-200/60 bg-white shadow-2xs',
            sidebarCollapsed ? 'justify-center p-1.5' : 'gap-3 px-2.5 py-2',
          )}>
            <div
              title={sidebarCollapsed ? (user?.fullName || 'Người dùng') : undefined}
              className="size-8 rounded-full bg-primary/10 text-primary font-bold text-xs flex items-center justify-center shrink-0 ring-1 ring-primary/20"
            >
              {userInitial}
            </div>
            {!sidebarCollapsed && (
              <>
                <div className="flex-1 min-w-0">
                  <div className="text-xs font-semibold text-slate-900 truncate">
                    {user?.fullName || 'Người dùng'}
                  </div>
                  <div className="text-[11px] text-slate-500 truncate">{primaryRoleLabel}</div>
                </div>
                <button
                  type="button"
                  onClick={() => void logout()}
                  disabled={isLoggingOut}
                  title="Đăng xuất khỏi hệ thống"
                  aria-label="Đăng xuất khỏi hệ thống"
                  className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors cursor-pointer disabled:opacity-50"
                >
                  <LogOut className="size-4" />
                </button>
              </>
            )}
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top Translucent Header */}
        <header className="h-16 bg-white/80 backdrop-blur-md border-b border-slate-200/75 px-4 lg:px-8 flex items-center justify-between gap-4 sticky top-0 z-30">
          <div className="flex items-center gap-3 min-w-0">
            <button
              type="button"
              onClick={() => setSidebarOpen(true)}
              aria-label="Mở menu điều hướng"
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
        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
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
