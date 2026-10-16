/**
 * DTO đại diện cho một quyền hạn (Permission) trong hệ thống
 */
export interface PermissionDto {
  id: number;
  permissionCode: string;
  permissionName: string | null;
  module: string | null;
}

/**
 * Danh mục quyền được gom nhóm theo từng phân hệ nghiệp vụ (Module)
 */
export interface ModulePermissionsDto {
  module: string;
  permissions: PermissionDto[];
}

/**
 * Thông tin chi tiết một Vai trò kèm danh sách quyền hiện tại
 */
export interface RolePermissionsDto {
  roleId: number;
  roleName: string;
  description: string | null;
  permissions: PermissionDto[];
}

/**
 * Payload yêu cầu cập nhật phân quyền cho một vai trò
 */
export interface UpdateRolePermissionsRequest {
  permissionIds: number[];
}

/**
 * Metadata thông tin hiển thị của 6 Vai trò hệ thống
 */
export interface SystemRoleInfo {
  id: number;
  roleName: string;
  title: string;
  description: string;
  badge?: string;
}

