import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { authEvents } from './authEvents';
import { tokenStorage } from './tokenStorage';
import type { ApiResponse, AuthTokens } from './types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5287';
const REFRESH_TOKEN_URL = '/api/identity/auth/refresh';

export const httpClient = axios.create({
  baseURL: API_BASE_URL,
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
  (error: unknown) => Promise.reject(error)
);

// RESPONSE INTERCEPTOR: Handle 401 & Refresh Token Queue
httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    if (!originalRequest || !error.response || error.response.status !== 401) {
      return Promise.reject(error);
    }

    // request was to the refresh token but still 401
    if (originalRequest.url?.includes(REFRESH_TOKEN_URL)) {
      tokenStorage.clearTokens();
      authEvents.emitUnauthenticated();
      return Promise.reject(error);
    }

    if (originalRequest._retry) {
      tokenStorage.clearTokens();
      authEvents.emitUnauthenticated();
      return Promise.reject(error);
    }

    // push request into queue (if any)
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

    const refreshToken = tokenStorage.getRefreshToken();

    if (!refreshToken) {
      isRefreshing = false;
      tokenStorage.clearTokens();
      authEvents.emitUnauthenticated();
      return Promise.reject(error);
    }

    try {
      const response = await axios.post<ApiResponse<AuthTokens> | AuthTokens>(
        `${API_BASE_URL}${REFRESH_TOKEN_URL}`,
        { refreshToken },
        { headers: { 'Content-Type': 'application/json' } }
      );

      const data = response.data;
      const tokens: AuthTokens = 'data' in data && data.data ? data.data : (data as AuthTokens);

      if (!tokens.accessToken || !tokens.refreshToken) {
        throw new Error('Invalid token response during refresh');
      }

      tokenStorage.setTokens(tokens.accessToken, tokens.refreshToken);
      processQueue(null, tokens.accessToken);

      if (originalRequest.headers) {
        originalRequest.headers.Authorization = `Bearer ${tokens.accessToken}`;
      }

      return httpClient(originalRequest);
    } catch (refreshError: unknown) {
      processQueue(refreshError, null);
      tokenStorage.clearTokens();
      authEvents.emitUnauthenticated();

      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  }
);
