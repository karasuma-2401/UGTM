import { useQuery } from '@tanstack/react-query';
import { rolePermissionApi } from '../services/rolePermissionApi';
import { SYSTEM_ROLES } from '../constants/systemRoles';

export function useRoles(includeHidden = false, enabled = true) {
  return useQuery({
    queryKey: ['identity-roles', includeHidden],
    enabled,
    queryFn: async () => {
      const result = await rolePermissionApi.getRoles(includeHidden);
      if (!result.success) throw new Error(result.error?.message || 'Không thể tải danh sách vai trò.');
      return result.data;
    },
  });
}

export function roleDisplay(role: { roleId: number; roleName: string; description: string | null }) {
  const system = SYSTEM_ROLES.find((item) => item.roleName === role.roleName);
  return { id: role.roleId, roleName: role.roleName, title: system?.title ?? role.roleName, description: role.description ?? '' };
}
