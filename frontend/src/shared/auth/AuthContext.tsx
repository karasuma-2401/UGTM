import React, { createContext, useEffect, useState, useCallback } from 'react';
import { authEvents, tokenStorage } from '../api';
import type { UserProfile } from '@/features/auth/types/auth.types';

const USER_PROFILE_KEY = 'ugtm_user_profile';

export interface AuthContextType {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (accessToken: string, refreshToken: string, user: UserProfile) => void;
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
    localStorage.removeItem(USER_PROFILE_KEY);
    setUser(null);
  }, []);

  const login = useCallback(
    (accessToken: string, refreshToken: string, userProfile: UserProfile) => {
      tokenStorage.setTokens(accessToken, refreshToken);
      localStorage.setItem(USER_PROFILE_KEY, JSON.stringify(userProfile));
      setUser(userProfile);
    },
    [],
  );

  const hasRole = useCallback(
    (roles: string | string[]): boolean => {
      if (!user || !user.roles || user.roles.length === 0) return false;
      const roleArray = Array.isArray(roles) ? roles : [roles];
      return roleArray.some((role) => user.roles.includes(role));
    },
    [user],
  );

  const hasPermission = useCallback(
    (permissions: string | string[]): boolean => {
      if (!user || !user.permissions || user.permissions.length === 0) return false;
      const permArray = Array.isArray(permissions) ? permissions : [permissions];
      return permArray.some((perm) => user.permissions.includes(perm));
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
