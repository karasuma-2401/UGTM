import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';

export interface NotificationItem {
  id: string;
  title: string;
  content: string;
  notificationType: string;
  priority: 'Normal' | 'Critical' | string;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
  actionPath?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  academicBatchId?: string | null;
  sourceModule: string;
}

export type NotificationPage = PagedResult<NotificationItem>;

export interface UnreadCountResponse {
  unreadCount: number;
}

export interface MarkAllNotificationsReadResponse {
  updatedCount: number;
}

export interface DeviceRegistrationResponse {
  id: string;
  installationId: string;
  platform: string;
  lastSeenAt: string;
  isActive: boolean;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Không thể xử lý yêu cầu thông báo.');
  }
  return response.data;
}

export const notificationApi = {
  async getNotifications(
    params?: { pageNumber?: number; pageSize?: number; unreadOnly?: boolean },
    signal?: AbortSignal,
  ): Promise<NotificationPage> {
    const response = await httpClient.get<ApiResponse<NotificationPage>>('/api/notifications', {
      params: {
        pageNumber: params?.pageNumber ?? 1,
        pageSize: params?.pageSize ?? 20,
        unreadOnly: params?.unreadOnly ?? false,
      },
      signal,
    });
    return unwrap(response.data);
  },

  async getUnreadCount(signal?: AbortSignal): Promise<number> {
    const response = await httpClient.get<ApiResponse<UnreadCountResponse>>(
      '/api/notifications/unread-count',
      { signal },
    );
    return unwrap(response.data).unreadCount;
  },

  async markAsRead(id: string): Promise<void> {
    const response = await httpClient.patch<ApiResponse<unknown>>(
      `/api/notifications/${id}/read`,
    );
    if (!response.data.success) {
      throw new Error(response.data.error.message || 'Không thể đánh dấu đã đọc.');
    }
  },

  async markAllAsRead(): Promise<number> {
    const response = await httpClient.patch<ApiResponse<MarkAllNotificationsReadResponse>>(
      '/api/notifications/read-all',
    );
    return unwrap(response.data).updatedCount;
  },

  async registerDevice(payload: {
    installationId: string;
    token: string;
    platform?: 'web' | 'android' | 'ios';
  }): Promise<DeviceRegistrationResponse> {
    const response = await httpClient.post<ApiResponse<DeviceRegistrationResponse>>(
      '/api/notifications/devices',
      payload,
    );
    return unwrap(response.data);
  },

  async revokeDevice(installationId: string): Promise<void> {
    const response = await httpClient.delete<ApiResponse<unknown>>(
      `/api/notifications/devices/${encodeURIComponent(installationId)}`,
    );
    if (!response.data.success) {
      throw new Error(response.data.error.message || 'Không thể thu hồi thiết bị thông báo.');
    }
  },
};
