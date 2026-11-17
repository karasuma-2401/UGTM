import axios from 'axios';
import type { LoginData } from '@/features/auth/types/auth.types';
import { API_BASE_URL } from './config';
import { CSRF_HEADER_NAME, getCsrfToken } from './csrf';
import { tokenStorage } from './tokenStorage';
import type { ApiResponse } from './types';

let pendingRefresh: Promise<ApiResponse<LoginData>> | null = null;
let refreshBlocked = false;

export function allowSessionRefresh(): void {
  refreshBlocked = false;
}

// Startup and Axios retries share one refresh because refresh cookies rotate once.
export function refreshSession(): Promise<ApiResponse<LoginData>> {
  if (refreshBlocked) return Promise.reject(new Error('Phiên đang đăng xuất.'));
  if (pendingRefresh) return pendingRefresh;
  pendingRefresh = (async () => {
    const csrfToken = await getCsrfToken(true);
    const response = await axios.post<ApiResponse<LoginData>>(
      `${API_BASE_URL}/api/identity/auth/refresh`,
      {},
      {
        withCredentials: true,
        timeout: 15_000,
        headers: { 'Content-Type': 'application/json', [CSRF_HEADER_NAME]: csrfToken },
      },
    );
    const result = response.data;
    if (!result.success || !result.data?.accessToken) {
      throw new Error(result.error?.message || 'Không thể làm mới phiên đăng nhập.');
    }
    if (refreshBlocked) throw new Error('Phiên đang đăng xuất.');
    tokenStorage.setAccessToken(result.data.accessToken);
    localStorage.setItem('ugtm_user_profile', JSON.stringify(result.data.user));
    return result;
  })().finally(() => {
    pendingRefresh = null;
  });
  return pendingRefresh;
}

export async function stopSessionRefresh(): Promise<void> {
  refreshBlocked = true;
  // Let a rotating cookie arrive before logout deletes it on the server.
  try {
    await pendingRefresh;
  } catch {
    /* Logout must still run. */
  }
}

export function isInvalidSession(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401;
}
