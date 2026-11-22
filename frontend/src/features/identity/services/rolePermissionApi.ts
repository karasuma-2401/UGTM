import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  ModulePermissionsDto,
  RolePermissionsDto,
  UpdateRolePermissionsRequest,
  RoleSummaryDto,
} from '../types/rolePermission.types';

export const rolePermissionApi = {
  async updateDescription(id: number, description: string): Promise<ApiResponse<RoleSummaryDto>> {
    const response = await httpClient.put<ApiResponse<RoleSummaryDto>>(`/api/roles/${id}/description`, { description });
    return response.data;
  },
  async getRoles(includeHidden = false): Promise<ApiResponse<RoleSummaryDto[]>> {
    const response = await httpClient.get<ApiResponse<RoleSummaryDto[]>>('/api/roles', { params: { includeHidden } });
    return response.data;
  },
  async createRole(roleName: string, description: string): Promise<ApiResponse<RolePermissionsDto>> {
    const response = await httpClient.post<ApiResponse<RolePermissionsDto>>('/api/roles', { roleName, description, permissionIds: [] });
    return response.data;
  },
  async setVisibility(id: number, isHidden: boolean): Promise<ApiResponse<RoleSummaryDto>> {
    const response = await httpClient.patch<ApiResponse<RoleSummaryDto>>(`/api/roles/${id}/visibility`, { isHidden });
    return response.data;
  },
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

