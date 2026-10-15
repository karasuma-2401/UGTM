import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { authEvents } from './authEvents';
import { clearCsrfToken, CSRF_HEADER_NAME, getCsrfToken } from './csrf';
import { tokenStorage } from './tokenStorage';
import type { ApiResponse } from './types';
import type { LoginData } from '@/features/auth/types/auth.types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5287';
const REFRESH_TOKEN_URL = '/api/identity/auth/refresh';

export const httpClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

interface QueueItem {
  resolve: (token: string) => void;
  reject: (error: unknown) => void;
}

let isRefreshing = false;
let failedQueue: QueueItem[] = [];

const processQueue = (error: unknown, token: string | null = null): void => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else if (token) {
      prom.resolve(token);
    }
  });

  failedQueue = [];
};

// REQUEST INTERCEPTOR: Attach Access Token
httpClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const accessToken = tokenStorage.getAccessToken();

    if (accessToken && config.headers) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }

    return config;
  },
  (error: unknown) => Promise.reject(error),
);

// RESPONSE INTERCEPTOR: Handle 401 & Refresh Token Queue
httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as
      (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    if (!originalRequest || !error.response || error.response.status !== 401) {
      return Promise.reject(error);
    }

    // Request was to the refresh token endpoint but still 401 -> session invalid
    if (originalRequest.url?.includes(REFRESH_TOKEN_URL)) {
      tokenStorage.clearTokens();
      clearCsrfToken();
      authEvents.emitUnauthenticated();
      return Promise.reject(error);
    }

    if (originalRequest._retry) {
      tokenStorage.clearTokens();
      clearCsrfToken();
      authEvents.emitUnauthenticated();
      return Promise.reject(error);
    }

    // Push request into queue if refresh is already in progress
    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        failedQueue.push({ resolve, reject });
      })
        .then((newToken: string) => {
          if (originalRequest.headers) {
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
          }
          return httpClient(originalRequest);
        })
        .catch((err: unknown) => Promise.reject(err));
    }

    originalRequest._retry = true;
    isRefreshing = true;

    try {
      // Backend refresh endpoint requires CSRF token and sends HttpOnly refresh cookie automatically
      const csrfToken = await getCsrfToken(true);
      const response = await axios.post<ApiResponse<LoginData>>(
        `${API_BASE_URL}${REFRESH_TOKEN_URL}`,
        {},
        {
          withCredentials: true,
          headers: {
            'Content-Type': 'application/json',
            [CSRF_HEADER_NAME]: csrfToken,
          },
        },
      );

      const data = response.data;
      if (!data.success || !data.data?.accessToken) {
        throw new Error(data.error?.message || 'Phiên đăng nhập đã hết hạn.');
      }

      const newAccessToken = data.data.accessToken;
      tokenStorage.setAccessToken(newAccessToken);

      if (data.data.user) {
        localStorage.setItem('ugtm_user_profile', JSON.stringify(data.data.user));
      }

      processQueue(null, newAccessToken);

      if (originalRequest.headers) {
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
      }

      return httpClient(originalRequest);
    } catch (refreshError: unknown) {
      clearCsrfToken();
      processQueue(refreshError, null);
      tokenStorage.clearTokens();
      authEvents.emitUnauthenticated();

      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  },
);
