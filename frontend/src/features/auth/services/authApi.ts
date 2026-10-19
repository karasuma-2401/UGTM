import axios from 'axios';

import { API_BASE_URL } from '@/shared/api/config';
import { clearCsrfToken, CSRF_HEADER_NAME, getCsrfToken } from '@/shared/api/csrf';
import { httpClient } from '@/shared/api/httpClient';
import type { ApiResponse } from '@/shared/api/types';

import type {
  ChangePasswordRequest,
  LoginData,
  LoginRequest,
  UserProfile,
} from '../types/auth.types';

const AUTH_BASE_PATH = '/api/identity/auth';

// Public auth operations (login, refresh, logout) must use withCredentials: true
// to receive and send HttpOnly refresh and csrf cookies, and bypass Bearer interceptor.
const publicAuthClient = axios.create({
  baseURL: API_BASE_URL,
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
  const csrfToken = await getCsrfToken(true);
  const response = await publicAuthClient.post<ApiResponse<LoginData>>(
    `${AUTH_BASE_PATH}/refresh`,
    {},
    {
      headers: {
        [CSRF_HEADER_NAME]: csrfToken,
      },
    },
  );
  return response.data;
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
  const response = await httpClient.get<ApiResponse<UserProfile>>(`${AUTH_BASE_PATH}/me`);
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

// --- Account Activation APIs ---

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

// --- Password Recovery APIs ---

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
