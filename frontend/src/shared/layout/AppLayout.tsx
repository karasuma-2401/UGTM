import { useState, useMemo } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import ugtmLogo from '../../../assets/logo_UGTM.svg';
import {
  Bell,
  Search,
  Menu,
  X,
  LogOut,
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

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const location = useLocation();
  const { user, logout, isLoggingOut, hasPermission, hasRole } = useAuth();

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

  const userInitial = user?.fullName ? user.fullName.trim().charAt(0).toUpperCase() : 'U';
  const totalVisibleItems = visibleSections.reduce((acc, s) => acc + s.items.length, 0);

  return (
    <div className="min-h-screen bg-slate-50/60 flex">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-slate-900/40 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-50 w-64 bg-white border-r border-slate-200 flex flex-col transition-transform duration-200 ease-in-out lg:static lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        {/* Sidebar Header / Brand */}
        <div className="h-16 flex items-center justify-between px-4 border-b border-slate-100">
          <Link to="/" className="flex items-center gap-2.5 min-w-0 group">
            <div className="h-9 px-2 flex items-center justify-center shrink-0 shadow-2xs group-hover:border-slate-300 transition-colors">
              <img src={ugtmLogo} alt="UGTM Logo" className="h-4.5 w-auto object-contain" />
            </div>
            <div className="min-w-0">
              <div className="font-bold text-sm tracking-tight text-slate-900 truncate">
                UGTM Portal
              </div>
              <div className="text-[11px] text-slate-500 font-medium truncate">
                Quản lý Khóa luận ĐH
              </div>
            </div>
          </Link>
          <button
            onClick={() => setSidebarOpen(false)}
            className="lg:hidden p-1.5 rounded-md text-slate-400 hover:text-slate-600 hover:bg-slate-100"
          >
            <X className="size-5" />
          </button>
        </div>

        <CurrentAcademicBatch />

        {/* Navigation sections & items */}
        <div className="flex-1 overflow-y-auto px-3 py-2 space-y-4">
          {totalVisibleItems === 0 ? (
            <div className="px-3 py-6 text-center text-xs text-slate-400 font-medium">
              Không có phân hệ nào khả dụng cho tài khoản này
            </div>
          ) : (
            visibleSections.map((section) => (
              <div key={section.id} className="space-y-1">
                {section.title && (
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
                      style={isActive ? { background: 'var(--gradient-brand)' } : undefined}
                      className={cn(
                        'flex items-center justify-between px-3 py-2 rounded-lg text-sm font-medium transition-all group',
                        isActive
                          ? 'text-white shadow-sm'
                          : 'text-slate-600 hover:bg-slate-100/80 hover:text-slate-900',
                      )}
                    >
                      <div className="flex items-center gap-3">
                        <Icon
                          className={cn(
                            'size-4.5 shrink-0 transition-colors',
                            isActive ? 'text-white' : 'text-slate-400 group-hover:text-slate-700',
                          )}
                        />
                        <span>{item.label}</span>
                      </div>
                      {item.badge && !isActive && (
                        <Badge variant="secondary" className="text-[10px] px-1.5 py-0">
                          {item.badge}
                        </Badge>
                      )}
                    </NavLink>
                  );
                })}
              </div>
            ))
          )}
        </div>

        {/* Sidebar Footer User profile preview */}
        <div className="p-3 border-t border-slate-150 bg-slate-50/50">
          <div className="flex items-center gap-3 px-2 py-1.5 rounded-md">
            <div className="size-8 rounded-full bg-primary/10 text-primary font-semibold text-xs flex items-center justify-center">
              {userInitial}
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-xs font-semibold text-slate-900 truncate">
                {user?.fullName || 'Người dùng'}
              </div>
              <div className="text-[11px] text-slate-500 truncate">
                {user?.roles?.join(', ') || user?.email || 'Thành viên'}
              </div>
            </div>
            <button
              type="button"
              onClick={() => void logout()}
              disabled={isLoggingOut}
              title="Đăng xuất khỏi hệ thống"
              className="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-md transition-colors"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top Header */}
        <header className="h-16 bg-white border-b border-slate-200 px-4 lg:px-8 flex items-center justify-between sticky top-0 z-30">
          <div className="flex items-center gap-3 flex-1 max-w-md">
            <button
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden p-2 rounded-lg text-slate-500 hover:bg-slate-100"
            >
              <Menu className="size-5" />
            </button>
            <div className="w-full hidden sm:block">
              <Input
                type="text"
                placeholder="Tìm nhanh sinh viên, đề tài, giảng viên... (⌘K)"
                leftIcon={<Search className="size-4 text-slate-400" />}
                className="bg-slate-50/60 focus:bg-white border-slate-200"
              />
            </div>
          </div>

          {/* Header Right Actions */}
          <div className="flex items-center gap-2 sm:gap-3">
            <Button variant="ghost" size="icon" className="relative text-slate-500">
              <Bell className="size-4.5" />
              <span className="size-2 rounded-full bg-rose-500 absolute top-2 right-2 ring-2 ring-white" />
            </Button>

            <div className="h-4 w-px bg-slate-200 mx-1 hidden sm:block" />

            <div className="flex items-center gap-2 pl-1">
              <div className="size-8 rounded-full bg-primary/10 text-primary font-bold text-xs flex items-center justify-center ring-1 ring-primary/20">
                {userInitial}
              </div>
              <div className="hidden md:block text-left">
                <div className="text-xs font-semibold text-slate-800">
                  {user?.fullName || 'Người dùng'}
                </div>
                <div className="text-[11px] text-slate-400">
                  {user?.roles?.join(', ') || 'Thành viên'}
                </div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => void logout()}
                disabled={isLoggingOut}
                className="text-slate-500 hover:text-rose-600 hover:bg-rose-50 gap-1.5 text-xs font-medium ml-1"
                title="Đăng xuất"
              >
                <LogOut className="size-3.5" />
                <span className="hidden sm:inline">Đăng xuất</span>
              </Button>
            </div>
          </div>
        </header>

        {/* Page Content Body */}
        <main className="flex-1 p-4 lg:p-8 max-w-7xl w-full mx-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
