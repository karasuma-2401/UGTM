import axios from 'axios';

import { httpClient } from '@/shared/api/httpClient';
import type { ApiResponse } from '@/shared/api/types';

import type {
  AuthTokens,
  ChangePasswordRequest,
  LoginData,
  LoginRequest,
  LogoutRequest,
  RefreshTokenRequest,
  UserProfile,
} from '../types/auth.types';

// Public auth operations must not trigger the authenticated client's 401 refresh flow.
const publicAuthClient = axios.create({
  baseURL: httpClient.defaults.baseURL,
  headers: { 'Content-Type': 'application/json' },
});

const AUTH_BASE_PATH = '/api/identity/auth';

export async function loginApi(payload: LoginRequest): Promise<ApiResponse<LoginData>> {
  const response = await publicAuthClient.post<ApiResponse<LoginData>>(
    `${AUTH_BASE_PATH}/login`,
    payload,
  );
  return response.data;
}

export async function refreshTokenApi(
  payload: RefreshTokenRequest,
): Promise<ApiResponse<AuthTokens>> {
  const response = await publicAuthClient.post<ApiResponse<AuthTokens>>(
    `${AUTH_BASE_PATH}/refresh`,
    payload,
  );
  return response.data;
}

export async function logoutApi(payload: LogoutRequest): Promise<ApiResponse<null>> {
  const response = await publicAuthClient.post<ApiResponse<null>>(
    `${AUTH_BASE_PATH}/logout`,
    payload,
  );
  return response.data;
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
