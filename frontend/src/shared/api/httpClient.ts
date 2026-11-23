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

interface ErrorPayloadShape {
  message?: unknown;
  detail?: unknown;
  title?: unknown;
  errors?: Record<string, unknown> | null;
  error?: {
    code?: unknown;
    message?: unknown;
    errors?: Record<string, unknown> | null;
  } | null;
}

function extractValidationErrors(errors: Record<string, unknown> | null | undefined): string | null {
  if (!errors || typeof errors !== 'object') return null;
  const messages: string[] = [];
  for (const value of Object.values(errors)) {
    if (Array.isArray(value)) {
      for (const entry of value) {
        if (typeof entry === 'string' && entry.trim()) {
          messages.push(entry.trim());
        }
      }
    } else if (typeof value === 'string' && value.trim()) {
      messages.push(value.trim());
    }
  }
  return messages.length > 0 ? messages.join(' ') : null;
}

function extractServerErrorMessage(data: unknown): string | null {
  if (!data) return null;
  if (typeof data === 'string') {
    const trimmed = data.trim();
    return trimmed && !trimmed.startsWith('<') ? trimmed : null;
  }
  if (typeof data !== 'object') return null;

  const payload = data as ErrorPayloadShape;
  if (typeof payload.error?.message === 'string' && payload.error.message.trim()) {
    return payload.error.message.trim();
  }
  const nestedValidation = extractValidationErrors(payload.error?.errors);
  if (nestedValidation) return nestedValidation;

  if (typeof payload.detail === 'string' && payload.detail.trim()) {
    return payload.detail.trim();
  }
  if (typeof payload.message === 'string' && payload.message.trim()) {
    return payload.message.trim();
  }
  const rootValidation = extractValidationErrors(payload.errors);
  if (rootValidation) return rootValidation;

  if (typeof payload.title === 'string' && payload.title.trim()) {
    return payload.title.trim();
  }
  return null;
}

function getHttpStatusFallback(status: number | undefined, hasResponse: boolean): string | null {
  if (!hasResponse) {
    return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối rồi thử lại.';
  }
  if (status === 400) return 'Yêu cầu không hợp lệ. Vui lòng kiểm tra lại dữ liệu.';
  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
  if (status === 404) return 'Không tìm thấy dữ liệu yêu cầu.';
  if (status === 409) return 'Thao tác bị xung đột với trạng thái dữ liệu hiện tại.';
  if (status === 413) return 'Tệp tải lên vượt quá dung lượng cho phép.';
  if (status === 422) return 'Dữ liệu gửi lên không hợp lệ.';
  if (typeof status === 'number' && status >= 500) {
    return 'Hệ thống đang gặp sự cố. Vui lòng thử lại sau.';
  }
  return null;
}

const RAW_AXIOS_MESSAGE_RE = /^(Request failed with status code \d+|Network Error)$/i;

export function getApiErrorMessage(
  error: unknown,
  fallback = 'Thao tác không thành công. Vui lòng thử lại.',
): string {
  if (!error) return fallback;

  if (typeof error === 'object') {
    const candidate = error as {
      isAxiosError?: boolean;
      response?: { status?: number; data?: unknown };
      message?: unknown;
    };
    const fromPayload = extractServerErrorMessage(candidate.response?.data);
    if (fromPayload) return fromPayload;

    if (candidate.isAxiosError || candidate.response) {
      const statusFallback = getHttpStatusFallback(
        candidate.response?.status,
        Boolean(candidate.response),
      );
      if (statusFallback) return statusFallback;
    }

    if (
      typeof candidate.message === 'string' &&
      candidate.message.trim() &&
      !RAW_AXIOS_MESSAGE_RE.test(candidate.message.trim())
    ) {
      return candidate.message.trim();
    }
  }

  return fallback;
}

async function normalizeAxiosError(error: unknown): Promise<void> {
  if (!axios.isAxiosError(error) || error.code === 'ERR_CANCELED') {
    return;
  }

  let data: unknown = error.response?.data;
  if (typeof Blob !== 'undefined' && data instanceof Blob && data.type.toLowerCase().includes('json')) {
    try {
      data = JSON.parse(await data.text());
    } catch {
      // Keep original blob if it is not valid JSON.
    }
  }

  const resolvedMessage =
    extractServerErrorMessage(data) ??
    getHttpStatusFallback(error.response?.status, Boolean(error.response));

  if (resolvedMessage) {
    try {
      error.message = resolvedMessage;
    } catch {
      // Ignore frozen error objects in tests.
    }
  }
}

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
      await normalizeAxiosError(error);
      return Promise.reject(error);
    }
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    if (original._retry) {
      expireSession();
      await normalizeAxiosError(error);
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
      await normalizeAxiosError(refreshError);
      return Promise.reject(refreshError);
    }
    return httpClient(original);
  },
);
