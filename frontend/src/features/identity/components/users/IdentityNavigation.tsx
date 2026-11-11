import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { SelectionIndicator } from '@/components/ui/selection-indicator';
import { GraduationCap, ShieldCheck, UsersRound } from 'lucide-react';
import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';
import { cn } from '@/lib/utils';

export function IdentityNavigation() {
  const location = useLocation();
  const navigate = useNavigate();
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
      className="relative isolate flex overflow-x-auto"
    >
      <SelectionIndicator
        activeKey={location.pathname}
        selector='a[aria-current="page"]'
        continuityKey="identity-tabs"
        className="bg-primary/5 after:absolute after:inset-x-0 after:bottom-0 after:h-0.5 after:content-[''] after:[background-image:var(--gradient-brand)]"
      />
      {tabs
        .filter((tab) => tab.visible)
        .map(({ path, label, icon: Icon }) => (
          <NavLink
            key={path}
            data-selection-item
            to={`/modules/identity/${path}`}
            onClick={(event) => {
              // Commit the tab and its loading UI without deferring the route render.
              // Modified clicks retain native link behavior; router blockers still apply.
              if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
                return;
              event.preventDefault();
              if (location.pathname !== `/modules/identity/${path}`)
                void navigate(`/modules/identity/${path}`, { flushSync: true });
            }}
            className={({ isActive }) =>
              cn(
                'relative z-10 inline-flex h-12 min-w-0 flex-1 items-center justify-center gap-2 border-b-2 border-transparent px-2 text-center text-xs font-medium transition-colors duration-150 sm:flex-none sm:px-4 sm:text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
                isActive
                  ? 'text-primary hover:text-primary'
                  : 'border-transparent text-slate-500 hover:bg-slate-100/60 hover:text-slate-800',
              )
            }
          >
            <Icon className="hidden size-4 shrink-0 sm:block" aria-hidden="true" />
            {label}
          </NavLink>
        ))}
    </nav>
  );
}
