export type SystemRole =
  'Admin' | 'Faculty' | 'AcademicAffairs' | 'Lecturer' | 'Student' | 'CouncilMember';

export type AccountStatus = 'PendingActivation' | 'Active' | 'Suspended' | 'Disabled';

export type AccountGroup = 'Student' | 'Staff';

/**
 * Thông tin tổng quan của một user nhận từ backend
 */
export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  userCode: string | null;
  phoneNumber: string | null;
  status: AccountStatus;
  roles: SystemRole[];
  createdAt: string;
  lastLoginAt: string | null;
}

export interface UserStatistics {
  total: number;
  pendingActivation: number;
  disabled: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface UsersQuery {
  accountGroup?: AccountGroup;
  keyword: string;
  role: SystemRole | '';
  status: AccountStatus | '';
  pageNumber: number;
  pageSize: number;
}

export interface ImportUsersResult {
  totalRows: number;
  importedCount: number;
  role: string;
  errors: { rowNumber: number; field: string; code: string; message: string }[];
}

/**
 * Dữ liệu nhận từ input trên ui
 */
export interface UserFormValues {
  email: string;
  fullName: string;
  userCode: string;
  phoneNumber: string;
  roles: SystemRole[];
}

export const STATUS_LABELS: Record<AccountStatus, string> = {
  PendingActivation: 'Chờ kích hoạt',
  Active: 'Hoạt động',
  Suspended: 'Tạm ngưng',
  Disabled: 'Vô hiệu hóa',
};
