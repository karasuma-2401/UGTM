import { useAuth } from '@/shared/auth';
import type { AccountGroup } from '../types/user.types';
import { useUserFilters } from './useUserFilters';
import { useUserData } from './useUserData';
import { useUserModals } from './useUserModals';
import { useUserActions } from './useUserActions';

export function useUsers(accountGroup: AccountGroup) {
  const { user: currentUser, hasPermission } = useAuth();
  const canManage = hasPermission('identity.users.manage');
  const canImport = hasPermission('identity.users.import');
  const isStudent = accountGroup === 'Student';

  const permissions = {
    canManage,
    canImport,
    isStudent,
    currentUserId: currentUser?.id,
  };

  const filters = useUserFilters();
  const modals = useUserModals();
  const data = useUserData(
    accountGroup,
    currentUser?.id,
    filters.filters,
    filters.pageNumber,
    filters.pageSize,
  );
  const actions = useUserActions({
    canManage,
    currentUserId: currentUser?.id,
    onFormClose: () => modals.setIsFormOpen(false),
    onSetNotice: modals.setNotice,
    onResetPage: () => filters.setPageNumber(1),
    onSetEditingUser: modals.setEditingUser,
    onOpenForm: () => modals.setIsFormOpen(true),
  });

  return {
    permissions,
    filters,
    data,
    modals,
    actions,
  };
}