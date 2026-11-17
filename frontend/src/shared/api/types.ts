export interface ApiError {
  code: string;
  message: string;
  errors: Record<string, string[]> | null;
}

export type ApiResponse<T> =
  { success: true; data: T; error: null } | { success: false; data: null; error: ApiError };

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
