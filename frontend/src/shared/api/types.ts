export interface ApiResponse<T> {
  isSuccess: boolean;
  data: T;
  error?: {
    code: string;
    message: string;
    details?: string[];
  };
  timestamp: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
}
