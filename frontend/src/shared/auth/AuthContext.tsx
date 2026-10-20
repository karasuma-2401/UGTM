import React, { createContext, useCallback, useEffect, useRef, useState } from 'react';
import { authEvents, clearCsrfToken, tokenStorage } from '../api';
import type { UserProfile } from '@/features/auth/types/auth.types';
import { logoutApi } from '@/features/auth/services/authApi';
import { allowSessionRefresh, isInvalidSession, stopSessionRefresh } from '../api/sessionRefresh';
import { restoreSession } from './restoreSession';

const USER_PROFILE_KEY = 'ugtm_user_profile';

export interface AuthContextType {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isLoggingOut: boolean;
  sessionError: string;
  sessionNotice: string;
  retrySession: () => void;
  login: (
    accessToken: string,
    userOrRefreshToken: UserProfile | string,
    optionalUser?: UserProfile,
  ) => void;
  logout: () => Promise<void>;
  hasRole: (roles: string | string[]) => boolean;
  hasPermission: (permissions: string | string[]) => boolean;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [sessionError, setSessionError] = useState('');
  const [sessionNotice, setSessionNotice] = useState('');
  const [attempt, setAttempt] = useState(0);
  const generation = useRef(0);
  const pendingLogout = useRef<Promise<void> | null>(null);
  const retrySession = useCallback(() => {
    setIsLoading(true);
    setSessionError('');
    setUser(null);
    setAttempt((value) => value + 1);
  }, []);

  const clearLocalSession = useCallback(() => {
    tokenStorage.clearTokens();
    clearCsrfToken();
    localStorage.removeItem(USER_PROFILE_KEY);
    setUser(null);
  }, []);

  const logout = useCallback((): Promise<void> => {
    if (pendingLogout.current) return pendingLogout.current;
    generation.current += 1;
    setIsLoggingOut(true);
    setSessionError('');
    setSessionNotice('');
    pendingLogout.current = (async () => {
      try {
        await stopSessionRefresh();
        const response = await logoutApi();
        if (!response.success) throw new Error('Không thể thu hồi phiên đăng nhập.');
      } catch {
        setSessionNotice(
          'Đã xóa đăng nhập trên thiết bị, nhưng chưa xác nhận thu hồi phiên trên máy chủ. Vui lòng kiểm tra kết nối.',
        );
      } finally {
        clearLocalSession();
        setIsLoading(false);
        setIsLoggingOut(false);
        pendingLogout.current = null;
      }
    })();
    return pendingLogout.current;
  }, [clearLocalSession]);

  const login = useCallback(
    (accessToken: string, userOrRefreshToken: UserProfile | string, optionalUser?: UserProfile) => {
      let userProfile: UserProfile;
      let refreshToken: string | undefined;

      if (typeof userOrRefreshToken === 'string') {
        refreshToken = userOrRefreshToken;
        userProfile = optionalUser!;
      } else {
        userProfile = userOrRefreshToken;
      }

      generation.current += 1;
      allowSessionRefresh();
      setSessionError('');
      setSessionNotice('');
      tokenStorage.setTokens(accessToken, refreshToken);
      if (userProfile) {
        localStorage.setItem(USER_PROFILE_KEY, JSON.stringify(userProfile));
        setUser(userProfile);
      }
    },
    [],
  );

  const hasRole = useCallback(
    (roles: string | string[]): boolean => {
      if (!user || !user.roles || user.roles.length === 0) return false;
      const roleArray = Array.isArray(roles) ? roles : [roles];
      return roleArray.some((role) =>
        user.roles.some((r) => r.toLowerCase() === role.toLowerCase()),
      );
    },
    [user],
  );

  const hasPermission = useCallback(
    (permissions: string | string[]): boolean => {
      if (!user) return false;
      if (user.roles && user.roles.some((r) => r.toLowerCase() === 'admin')) {
        return true;
      }
      if (!user.permissions || user.permissions.length === 0) return false;
      const permArray = Array.isArray(permissions) ? permissions : [permissions];
      return permArray.some((perm) =>
        user.permissions.some((p) => p.toLowerCase() === perm.toLowerCase()),
      );
    },
    [user],
  );

  useEffect(() => {
    let active = true;
    const currentGeneration = ++generation.current;
    const isCurrent = () => active && generation.current === currentGeneration;
    allowSessionRefresh();

    void restoreSession()
      .then((profile) => {
        if (!isCurrent()) return;
        localStorage.setItem(USER_PROFILE_KEY, JSON.stringify(profile));
        setUser(profile);
      })
      .catch((error: unknown) => {
        if (!isCurrent()) return;
        if (isInvalidSession(error)) {
          clearLocalSession();
        } else {
          setSessionError(
            'Chưa thể kiểm tra phiên đăng nhập. Vui lòng kiểm tra kết nối rồi thử lại.',
          );
        }
      })
      .finally(() => {
        if (isCurrent()) setIsLoading(false);
      });

    const unsubscribe = authEvents.onUnauthenticated(clearLocalSession);
    return () => {
      active = false;
      unsubscribe();
    };
  }, [attempt, clearLocalSession]);

  const value: AuthContextType = {
    user,
    isAuthenticated: Boolean(user && tokenStorage.hasTokens()),
    isLoading,
    isLoggingOut,
    sessionError,
    sessionNotice,
    retrySession,
    login,
    logout,
    hasRole,
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
