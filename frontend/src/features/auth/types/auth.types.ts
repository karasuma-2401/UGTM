export type OtpMethod = 'email' | 'sms';

export interface UserPermissionDetail {
  permissionCode: string;
  permissionName?: string | null;
  module?: string | null;
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  userCode?: string | null;
  roles: string[];
  permissions: string[];
  mustChangePassword?: boolean;
  phoneNumber?: string | null;
  status?: 'PendingActivation' | 'Active' | 'Suspended' | 'Disabled' | string;
  createdAt?: string | null;
  activatedAt?: string | null;
  lastLoginAt?: string | null;
  passwordChangedAt?: string | null;
  permissionDetails?: UserPermissionDetail[] | null;
}

export interface UpdateMyProfileRequest {
  phoneNumber: string | null;
}

export interface SessionItem {
  id: string;
  authenticatedAt: string;
  lastSeenAt: string;
  idleExpiresAt: string;
  absoluteExpiresAt: string;
  userAgentSummary: string | null;
  isCurrent: boolean;
}

export interface RevokeSessionsResult {
  revokedCount: number;
  currentSessionRevoked: boolean;
}

export type ActivityCategory = 'all' | 'auth' | 'password' | 'session' | 'profile';

export interface UserActivityDay {
  date: string;
  totalCount: number;
  successCount: number;
  alertCount: number;
}

export interface UserActivitySummary {
  days: UserActivityDay[];
  totalEvents: number;
  activeDaysCount: number;
  recentAlertCount7d: number;
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
