import { getCurrentUserApi, refreshTokenApi } from '@/features/auth/services/authApi';
import type { UserProfile } from '@/features/auth/types/auth.types';
import { tokenStorage } from '../api/tokenStorage';

let pendingRestore: Promise<UserProfile> | null = null;

// React StrictMode effects share the same startup request.
export function restoreSession(): Promise<UserProfile> {
  if (pendingRestore) return pendingRestore;
  pendingRestore = (async () => {
    if (!tokenStorage.hasTokens()) await refreshTokenApi();
    const response = await getCurrentUserApi();
    if (!response.success || !response.data) {
      throw new Error(response.error?.message || 'Không thể kiểm tra phiên đăng nhập.');
    }
    return response.data;
  })().finally(() => {
    pendingRestore = null;
  });
  return pendingRestore;
}
