import axios from 'axios';
import { API_BASE_URL } from './config';
import type { ApiResponse } from './types';
import type { CsrfTokenData } from '@/features/auth/types/auth.types';

const CSRF_ENDPOINT = '/api/identity/auth/csrf';

export const CSRF_HEADER_NAME = 'X-CSRF-TOKEN';

let csrfTokenCache: string | null = null;
let pendingCsrfPromise: Promise<string> | null = null;

export async function getCsrfToken(forceRefresh = false): Promise<string> {
  if (csrfTokenCache && !forceRefresh) {
    return csrfTokenCache;
  }

  if (pendingCsrfPromise && !forceRefresh) {
    return pendingCsrfPromise;
  }

  pendingCsrfPromise = (async () => {
    try {
      const response = await axios.get<ApiResponse<CsrfTokenData>>(
        `${API_BASE_URL}${CSRF_ENDPOINT}`,
        {
          withCredentials: true,
          headers: {
            'Content-Type': 'application/json',
          },
        },
      );

      const data = response.data;
      if (!data.success || !data.data?.token) {
        throw new Error(data.error?.message || 'Không thể lấy CSRF token từ hệ thống.');
      }

      csrfTokenCache = data.data.token;
      return csrfTokenCache;
    } finally {
      pendingCsrfPromise = null;
    }
  })();

  return pendingCsrfPromise;
}

export function clearCsrfToken(): void {
  csrfTokenCache = null;
  pendingCsrfPromise = null;
}
