export type OtpPurpose = 'activate' | 'reset';
export type OtpMethod = 'email' | 'sms';

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  userCode?: string | null;
  roles: string[];
  permissions: string[];
}

export interface AuthTokens {
  accessToken: string;
  refreshToken?: string;
  tokenType?: 'Bearer';
  expiresIn?: number;
  refreshTokenExpiresAt?: string;
}

export interface LoginRequest {
  identifier: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginData {
  accessToken: string;
  accessTokenExpiresAt?: string;
  user: UserProfile;
}

export interface RefreshTokenRequest {
  refreshToken?: string;
}

export interface LogoutRequest {
  refreshToken?: string;
}

export interface CsrfTokenData {
  token: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
