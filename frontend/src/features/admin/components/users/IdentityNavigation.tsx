import { NavLink } from 'react-router-dom';
import { GraduationCap, ShieldCheck, UsersRound } from 'lucide-react';
import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
import { cn } from '@/lib/utils';

export function IdentityNavigation() {
  const { hasPermission } = useAuth();
  const { canReadUsers, canManageRoles } = getIdentityAccess(hasPermission);
  const tabs = [
    { path: 'students', label: 'Sinh viên', icon: GraduationCap, visible: canReadUsers },
    { path: 'staff', label: 'Giảng viên & cán bộ', icon: UsersRound, visible: canReadUsers },
    { path: 'permissions', label: 'Phân quyền', icon: ShieldCheck, visible: canManageRoles },
  ];

  return (
    <nav
      aria-label="Tài khoản và phân quyền"
      className="flex gap-1 overflow-x-auto border-b border-slate-200"
    >
      {tabs
        .filter((tab) => tab.visible)
        .map(({ path, label, icon: Icon }) => (
          <NavLink
            key={path}
            to={`/modules/identity/${path}`}
            className={({ isActive }) =>
              cn(
                'inline-flex h-12 shrink-0 items-center justify-center gap-2 border-b-2 px-4 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
                isActive
                  ? 'border-primary bg-primary/5 text-primary hover:bg-primary/5 hover:text-primary'
                  : 'border-transparent text-slate-500 hover:bg-slate-100/60 hover:text-slate-800',
              )
            }
          >
            <Icon className="size-4" aria-hidden="true" />
            {label}
          </NavLink>
        ))}
    </nav>
  );
}
