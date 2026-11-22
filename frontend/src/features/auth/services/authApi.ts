import axios from 'axios';

import { API_BASE_URL } from '@/shared/api/config';
import { clearCsrfToken, CSRF_HEADER_NAME, getCsrfToken } from '@/shared/api/csrf';
import { httpClient } from '@/shared/api/httpClient';
import { refreshSession } from '@/shared/api/sessionRefresh';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import type { AuditLogItem } from '@/features/audit/types/auditLog.types';

import type {
  ActivityCategory,
  ChangePasswordRequest,
  LoginData,
  LoginRequest,
  RevokeSessionsResult,
  SessionItem,
  UpdateMyProfileRequest,
  UserActivitySummary,
  UserProfile,
} from '../types/auth.types';

const AUTH_BASE_PATH = '/api/identity/auth';

const publicAuthClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
});

export async function loginApi(payload: LoginRequest): Promise<ApiResponse<LoginData>> {
  const csrfToken = await getCsrfToken(true);
  const response = await publicAuthClient.post<ApiResponse<LoginData>>(
    `${AUTH_BASE_PATH}/login`,
    {
      identifier: payload.identifier.trim(),
      password: payload.password,
    },
    {
      headers: {
        [CSRF_HEADER_NAME]: csrfToken,
      },
    },
  );
  return response.data;
}

export async function refreshTokenApi(): Promise<ApiResponse<LoginData>> {
  return refreshSession();
}

export async function logoutApi(): Promise<ApiResponse<null>> {
  try {
    const csrfToken = await getCsrfToken();
    const response = await publicAuthClient.post<ApiResponse<null>>(
      `${AUTH_BASE_PATH}/logout`,
      {},
      {
        headers: {
          [CSRF_HEADER_NAME]: csrfToken,
        },
      },
    );
    return response.data;
  } finally {
    clearCsrfToken();
  }
}

export async function getCurrentUserApi(): Promise<ApiResponse<UserProfile>> {
  const response = await httpClient.get<ApiResponse<UserProfile>>(`${AUTH_BASE_PATH}/me`, {
    timeout: 15_000,
  });
  return response.data;
}

export async function updateMyProfileApi(
  payload: UpdateMyProfileRequest,
): Promise<ApiResponse<UserProfile>> {
  const response = await httpClient.patch<ApiResponse<UserProfile>>(
    `${AUTH_BASE_PATH}/me`,
    payload,
  );
  return response.data;
}

export async function getMySessionsApi(): Promise<ApiResponse<SessionItem[]>> {
  const response = await httpClient.get<ApiResponse<SessionItem[]>>(`${AUTH_BASE_PATH}/sessions`);
  return response.data;
}

export async function revokeMySessionApi(
  sessionId: string,
): Promise<ApiResponse<RevokeSessionsResult>> {
  const response = await httpClient.delete<ApiResponse<RevokeSessionsResult>>(
    `${AUTH_BASE_PATH}/sessions/${sessionId}`,
  );
  return response.data;
}

export async function revokeAllMySessionsApi(): Promise<ApiResponse<RevokeSessionsResult>> {
  const response = await httpClient.delete<ApiResponse<RevokeSessionsResult>>(
    `${AUTH_BASE_PATH}/sessions`,
  );
  return response.data;
}

export async function getMyActivityApi(options?: {
  pageNumber?: number;
  pageSize?: number;
  category?: ActivityCategory;
  date?: string | null;
}): Promise<ApiResponse<PagedResult<AuditLogItem>>> {
  const { pageNumber = 1, pageSize = 15, category = 'all', date = null } = options ?? {};
  const response = await httpClient.get<ApiResponse<PagedResult<AuditLogItem>>>(
    `${AUTH_BASE_PATH}/activity`,
    {
      params: {
        pageNumber,
        pageSize,
        ...(category !== 'all' ? { category } : {}),
        ...(date ? { date } : {}),
      },
    },
  );
  return response.data;
}

export async function getMyActivitySummaryApi(
  days = 112,
): Promise<ApiResponse<UserActivitySummary>> {
  const response = await httpClient.get<ApiResponse<UserActivitySummary>>(
    `${AUTH_BASE_PATH}/activity/summary`,
    { params: { days } },
  );
  return response.data;
}

export async function changePasswordApi(
  payload: ChangePasswordRequest,
): Promise<ApiResponse<null>> {
  const response = await httpClient.post<ApiResponse<null>>(
    `${AUTH_BASE_PATH}/change-password`,
    payload,
  );
  return response.data;
}

export interface ActivationValidateData {
  maskedEmail: string;
  fullName: string;
  expiresAt: string;
}

export async function validateActivationApi(
  token: string,
): Promise<ApiResponse<ActivationValidateData>> {
  const response = await publicAuthClient.post<ApiResponse<ActivationValidateData>>(
    `${AUTH_BASE_PATH}/activation/validate`,
    { token },
  );
  return response.data;
}

export async function completeActivationApi(
  token: string,
  password: string,
): Promise<ApiResponse<null>> {
  const response = await publicAuthClient.post<ApiResponse<null>>(
    `${AUTH_BASE_PATH}/activation/complete`,
    { token, password },
  );
  return response.data;
}

export async function resendActivationApi(identifier: string): Promise<ApiResponse<null>> {
  const response = await publicAuthClient.post<ApiResponse<null>>(
    `${AUTH_BASE_PATH}/activation/resend`,
    { identifier },
  );
  return response.data;
}

export interface RecoveryStartData {
  challengeId: string;
  expiresAt: string;
}

export interface RecoveryVerifyData {
  resetGrant: string;
  expiresAt: string;
}

export async function startRecoveryApi(email: string): Promise<ApiResponse<RecoveryStartData>> {
  const response = await publicAuthClient.post<ApiResponse<RecoveryStartData>>(
    `${AUTH_BASE_PATH}/recovery/start`,
    { email },
  );
  return response.data;
}

export async function verifyRecoveryOtpApi(
  challengeId: string,
  code: string,
): Promise<ApiResponse<RecoveryVerifyData>> {
  const response = await publicAuthClient.post<ApiResponse<RecoveryVerifyData>>(
    `${AUTH_BASE_PATH}/recovery/verify`,
    { challengeId, code },
  );
  return response.data;
}

export async function resetRecoveryPasswordApi(
  resetGrant: string,
  newPassword: string,
): Promise<ApiResponse<null>> {
  const response = await publicAuthClient.post<ApiResponse<null>>(
    `${AUTH_BASE_PATH}/recovery/reset`,
    { resetGrant, newPassword },
  );
  return response.data;
}
