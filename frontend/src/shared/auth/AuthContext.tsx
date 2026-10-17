import React, { createContext, useCallback, useEffect, useState } from 'react';
import { authEvents, clearCsrfToken, tokenStorage } from '../api';
import type { UserProfile } from '@/features/auth/types/auth.types';

const USER_PROFILE_KEY = 'ugtm_user_profile';

export interface AuthContextType {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (accessToken: string, userOrRefreshToken: UserProfile | string, optionalUser?: UserProfile) => void;
  logout: () => void;
  hasRole: (roles: string | string[]) => boolean;
  hasPermission: (permissions: string | string[]) => boolean;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const logout = useCallback(() => {
    tokenStorage.clearTokens();
    clearCsrfToken();
    localStorage.removeItem(USER_PROFILE_KEY);
    setUser(null);
  }, []);

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
    const initializeAuth = () => {
      if (tokenStorage.hasTokens()) {
        const storedProfile = localStorage.getItem(USER_PROFILE_KEY);
        if (storedProfile) {
          try {
            const parsedUser = JSON.parse(storedProfile) as UserProfile;
            setUser(parsedUser);
          } catch {
            logout();
          }
        }
      } else {
        logout();
      }
      setIsLoading(false);
    };

    initializeAuth();

    // Subscribe to automatic 401 logout events from Axios Interceptor
    const unsubscribe = authEvents.onUnauthenticated(() => {
      logout();
    });

    return () => {
      unsubscribe();
    };
  }, [logout]);

  const value: AuthContextType = {
    user,
    isAuthenticated: Boolean(user && tokenStorage.hasTokens()),
    isLoading,
    login,
    logout,
    hasRole,
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
