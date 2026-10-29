import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  ModulePermissionsDto,
  RolePermissionsDto,
  UpdateRolePermissionsRequest,
} from '../types/rolePermission.types';

export const rolePermissionApi = {
  async getPermissions(): Promise<ApiResponse<ModulePermissionsDto[]>> {
    const response = await httpClient.get<ApiResponse<ModulePermissionsDto[]>>('/api/permissions');
    return response.data;
  },

  async getRolePermissions(roleId: number): Promise<ApiResponse<RolePermissionsDto>> {
    const response = await httpClient.get<ApiResponse<RolePermissionsDto>>(
      `/api/roles/${roleId}/permissions`,
    );
    return response.data;
  },

  async updateRolePermissions(
    roleId: number,
    permissionIds: number[],
  ): Promise<ApiResponse<RolePermissionsDto>> {
    const payload: UpdateRolePermissionsRequest = { permissionIds };
    const response = await httpClient.put<ApiResponse<RolePermissionsDto>>(
      `/api/roles/${roleId}/permissions`,
      payload,
    );
    return response.data;
  },
};

