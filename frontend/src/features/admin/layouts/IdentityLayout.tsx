import { Navigate, NavLink, Outlet } from 'react-router-dom';
import { GraduationCap, ShieldCheck, UsersRound } from 'lucide-react';

import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
import { cn } from '@/lib/utils';

export function IdentityIndexRedirect() {
  const { hasPermission } = useAuth();
  const { canReadUsers, canManageRoles } = getIdentityAccess(hasPermission);

  return (
    <Navigate to={canReadUsers ? 'students' : canManageRoles ? 'permissions' : '/403'} replace />
  );
}

export function IdentityLayout() {
  const { hasPermission } = useAuth();
  const { canReadUsers, canManageRoles, canAccess } = getIdentityAccess(hasPermission);

  if (!canAccess) return <Navigate to="/403" replace />;

  const tabs = [
    { path: 'students', label: 'Sinh viên', icon: GraduationCap, visible: canReadUsers },
    { path: 'staff', label: 'Giảng viên & cán bộ', icon: UsersRound, visible: canReadUsers },
    {
      path: 'permissions',
      label: 'Phân quyền',
      icon: ShieldCheck,
      visible: canManageRoles,
    },
  ];

  return (
    <div className="space-y-6">
      <nav
        aria-label="Tài khoản và phân quyền"
        className="flex gap-2 overflow-x-auto border-b border-border"
      >
        {tabs
          .filter((tab) => tab.visible)
          .map(({ path, label, icon: Icon }) => (
            <NavLink
              key={path}
              to={path}
              className={({ isActive }) =>
                cn(
                  'flex shrink-0 items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
                  isActive
                    ? 'border-primary text-primary'
                    : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
                )
              }
            >
              <Icon className="size-4" aria-hidden="true" />
              {label}
            </NavLink>
          ))}
      </nav>
      <Outlet />
    </div>
  );
}
