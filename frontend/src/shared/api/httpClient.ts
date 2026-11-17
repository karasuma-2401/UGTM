import axios, { type InternalAxiosRequestConfig } from 'axios';
import { API_BASE_URL } from './config';
import { authEvents } from './authEvents';
import { clearCsrfToken } from './csrf';
import { isInvalidSession, refreshSession } from './sessionRefresh';
import { tokenStorage } from './tokenStorage';

export const httpClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
});

function expireSession() {
  tokenStorage.clearTokens();
  clearCsrfToken();
  authEvents.emitUnauthenticated();
}

httpClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401 || !error.config) {
      return Promise.reject(error);
    }
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    if (original._retry) {
      expireSession();
      return Promise.reject(error);
    }
    original._retry = true;
    const latestToken = tokenStorage.getAccessToken();
    if (latestToken && original.headers.Authorization !== `Bearer ${latestToken}`) {
      original.headers.Authorization = `Bearer ${latestToken}`;
      return httpClient(original);
    }
    try {
      const response = await refreshSession();
      original.headers.Authorization = `Bearer ${response.data!.accessToken}`;
    } catch (refreshError) {
      // Outages and 5xx responses must not erase a potentially valid session.
      if (isInvalidSession(refreshError)) expireSession();
      return Promise.reject(refreshError);
    }
    return httpClient(original);
  },
);
