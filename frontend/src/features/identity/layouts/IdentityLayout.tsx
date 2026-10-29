import { Navigate, Outlet } from 'react-router-dom';

import { useAuth } from '@/shared/auth';
import { getIdentityAccess } from '@/shared/auth/identityAccess';

export function IdentityIndexRedirect() {
  const { hasPermission } = useAuth();
  const { canReadUsers, canManageRoles } = getIdentityAccess(hasPermission);

  return (
    <Navigate to={canReadUsers ? 'students' : canManageRoles ? 'permissions' : '/403'} replace />
  );
}

export function IdentityLayout() {
  const { hasPermission } = useAuth();
  const { canAccess } = getIdentityAccess(hasPermission);

  if (!canAccess) return <Navigate to="/403" replace />;

  return <Outlet />;
}
