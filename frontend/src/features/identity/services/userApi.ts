import axios from 'axios';

import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';

import type {
  AccountStatus,
  AccountGroup,
  ImportUsersResult,
  PagedResult,
  UserFormValues,
  UserSummary,
  UsersQuery,
  UserStatistics,
} from '../types/user.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message);
  }
  return response.data;
}

export function getUserErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const response = error.response?.data;

    if (response?.success === false) {
      return response.error.message;
    }
    if (error.response?.status === 403) {
      return 'Bạn không có quyền thực hiện thao tác này.';
    }
    if (!error.response) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối rồi thử lại.';
    }
  }

  return error instanceof Error ? error.message : 'Không thể thực hiện thao tác. Vui lòng thử lại.';
}

export const userApi = {
  async getStatistics(accountGroup: AccountGroup, signal?: AbortSignal): Promise<UserStatistics> {
    const [all, pending, disabled] = await Promise.all(
      (['', 'PendingActivation', 'Disabled'] as const).map((status) =>
        userApi.getUsers(
          {
            keyword: '',
            accountGroup,
            role: '',
            status,
            pageNumber: 1,
            pageSize: 1,
          },
          signal,
        ),
      ),
    );

    return {
      total: all.totalCount,
      pendingActivation: pending.totalCount,
      disabled: disabled.totalCount,
    };
  },

  async importUsers(file: File, role: 'Student' | 'Lecturer'): Promise<ImportUsersResult> {
    const data = new FormData();
    data.append('file', file);
    data.append('role', role);
    const response = await httpClient.post<ApiResponse<ImportUsersResult>>(
      '/api/users/import',
      data,
      {
        headers: { 'Content-Type': undefined },
      },
    );
    return unwrap(response.data);
  },

  async importStudents(file: File): Promise<ImportUsersResult> {
    return userApi.importUsers(file, 'Student');
  },

  async downloadUserTemplate(role: 'Student' | 'Lecturer'): Promise<Blob> {
    const response = await httpClient.get<Blob>('/api/users/import-template', {
      params: { role },
      responseType: 'blob',
    });
    return response.data;
  },

  async downloadStudentTemplate(): Promise<Blob> {
    return userApi.downloadUserTemplate('Student');
  },

  async getUsers(query: UsersQuery, signal?: AbortSignal): Promise<PagedResult<UserSummary>> {
    const response = await httpClient.get<ApiResponse<PagedResult<UserSummary>>>('/api/users', {
      params: {
        ...query,
        keyword: query.keyword.trim() || undefined,
        role: query.role || undefined,
        status: query.status || undefined,
      },
      signal,
    });

    return unwrap(response.data);
  },

  async createUser(values: UserFormValues): Promise<UserSummary> {
    const response = await httpClient.post<ApiResponse<UserSummary>>('/api/users', {
      email: values.email.trim(),
      fullName: values.fullName.trim(),
      userCode: values.userCode.trim() || null,
      phoneNumber: values.phoneNumber.trim() || null,
      roles: values.roles,
      sendActivationEmail: true,
    });

    return unwrap(response.data);
  },

  async updateUser(id: string, values: UserFormValues): Promise<UserSummary> {
    const response = await httpClient.put<ApiResponse<UserSummary>>(`/api/users/${id}`, {
      fullName: values.fullName.trim(),
      userCode: values.userCode.trim() || null,
      phoneNumber: values.phoneNumber.trim() || null,
      roles: values.roles,
    });

    return unwrap(response.data);
  },

  async updateStatus(
    id: string,
    status: Exclude<AccountStatus, 'PendingActivation'>,
  ): Promise<UserSummary> {
    const response = await httpClient.patch<ApiResponse<UserSummary>>(`/api/users/${id}/status`, {
      status,
    });

    return unwrap(response.data);
  },
};
