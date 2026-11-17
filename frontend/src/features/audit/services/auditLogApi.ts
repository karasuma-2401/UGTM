import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  AuditLogItem,
  AuditLogQueryParams,
  PagedAuditLogResult,
} from '../types/auditLog.types';

export interface AuditLogStatistics {
  total: number;
  success: number;
  denied: number;
  failed: number;
  other: number;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}

export function getAuditLogErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const response = error.response?.data;
    if (response?.success === false) return response.error.message;
    if (error.response?.status === 403) return 'Bạn không có quyền xem nhật ký thao tác.';
    if (!error.response) return 'Không thể kết nối đến máy chủ. Vui lòng thử lại.';
  }
  return error instanceof Error
    ? error.message
    : 'Không thể tải nhật ký thao tác. Vui lòng thử lại.';
}

// Date-only filters are calendar dates in Vietnam; the API uses an exclusive upper bound.
export function vietnamDateBoundary(value: string, nextDay: boolean): string {
  if (value.includes('T')) return new Date(value).toISOString();
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new Error('Ngày lọc không hợp lệ.');
  const date = new Date(`${value}T00:00:00+07:00`);
  if (Number.isNaN(date.getTime())) throw new Error('Ngày lọc không hợp lệ.');
  if (nextDay) date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString();
}

function queryParams(params: AuditLogQueryParams): Record<string, string | number> {
  const result: Record<string, string | number> = {};
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === '') continue;
    if (key === 'fromUtc' || key === 'toUtc')
      result[key] = vietnamDateBoundary(String(value), key === 'toUtc');
    else result[key] = value;
  }
  return result;
}

export const auditLogApi = {
  async getActions(signal?: AbortSignal): Promise<string[]> {
    const response = await httpClient.get<ApiResponse<string[]>>('/api/audit-logs/actions', {
      signal,
    });
    return unwrap(response.data);
  },
  async getAuditLogs(
    params: AuditLogQueryParams = {},
    signal?: AbortSignal,
  ): Promise<PagedAuditLogResult> {
    const response = await httpClient.get<ApiResponse<PagedAuditLogResult>>('/api/audit-logs', {
      params: queryParams(params),
      signal,
    });
    return unwrap(response.data);
  },
  async getStatistics(
    params: AuditLogQueryParams,
    signal?: AbortSignal,
  ): Promise<AuditLogStatistics> {
    const response = await httpClient.get<ApiResponse<AuditLogStatistics>>(
      '/api/audit-logs/statistics',
      { params: queryParams(params), signal },
    );
    return unwrap(response.data);
  },
  async getById(id: number, signal?: AbortSignal): Promise<AuditLogItem> {
    const response = await httpClient.get<ApiResponse<AuditLogItem>>(`/api/audit-logs/${id}`, {
      signal,
    });
    return unwrap(response.data);
  },
  async exportCsv(params: AuditLogQueryParams): Promise<void> {
    let response;
    try {
      response = await httpClient.get<Blob>('/api/audit-logs/export', {
        params: queryParams(params),
        responseType: 'blob',
      });
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
        try {
          const body = JSON.parse(await error.response.data.text());
          if (typeof body.message === 'string') throw new Error(body.message);
          if (typeof body.error?.message === 'string') throw new Error(body.error.message);
        } catch (parsedError) {
          if (parsedError instanceof Error && !(parsedError instanceof SyntaxError))
            throw parsedError;
        }
      }
      throw error;
    }
    const url = URL.createObjectURL(response.data);
    try {
      const link = document.createElement('a');
      link.href = url;
      link.download = 'audit-logs.csv';
      link.click();
    } finally {
      URL.revokeObjectURL(url);
    }
  },
};
