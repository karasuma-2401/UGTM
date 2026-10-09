export interface ApiError {
  code: string;
  message: string;
  errors: Record<string, string[]> | null;
}

export type ApiResponse<T> =
  { success: true; data: T; error: null } | { success: false; data: null; error: ApiError };
