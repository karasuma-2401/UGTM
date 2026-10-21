import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  AuditLogQueryParams,
  PagedAuditLogResult,
} from '../types/auditLog.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message);
  }
  return response.data;
}

export function getAuditLogErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const response = error.response?.data;
    if (response?.success === false) {
      return response.error.message;
    }
    if (error.response?.status === 403) {
      return 'Bạn không có quyền xem nhật ký thao tác (Chỉ dành cho Admin).';
    }
    if (!error.response) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng rồi thử lại.';
    }
  }

  return error instanceof Error ? error.message : 'Không thể tải nhật ký thao tác. Vui lòng thử lại sau.';
}

export const auditLogApi = {
  async getAuditLogs(
    params: AuditLogQueryParams = {},
    signal?: AbortSignal,
  ): Promise<PagedAuditLogResult> {
    const queryParams: Record<string, any> = {};

    if (params.actorUserId) queryParams.actorUserId = params.actorUserId;
    if (params.action) queryParams.action = params.action;
    if (params.targetType) queryParams.targetType = params.targetType;
    if (params.outcome) queryParams.outcome = params.outcome;
    if (params.pageNumber) queryParams.pageNumber = params.pageNumber;
    if (params.pageSize) queryParams.pageSize = params.pageSize;

    // Chuẩn hóa ngày sang chuẩn ISO UTC để Backend không báo lỗi 400 InvalidAuditLogFilter
    if (params.fromUtc) {
      const fromDate = params.fromUtc.includes('T')
        ? new Date(params.fromUtc)
        : new Date(`${params.fromUtc}T00:00:00.000Z`);
      if (!isNaN(fromDate.getTime())) {
        queryParams.fromUtc = fromDate.toISOString();
      }
    }

    if (params.toUtc) {
      const toDate = params.toUtc.includes('T')
        ? new Date(params.toUtc)
        : new Date(`${params.toUtc}T23:59:59.999Z`);
      if (!isNaN(toDate.getTime())) {
        queryParams.toUtc = toDate.toISOString();
      }
    }

    const response = await httpClient.get<ApiResponse<PagedAuditLogResult>>('/api/audit-logs', {
      params: queryParams,
      signal,
    });

    return unwrap(response.data);
  },
};

