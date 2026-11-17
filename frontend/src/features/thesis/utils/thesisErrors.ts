export type ThesisErrorKind = 'forbidden' | 'not_found' | 'load_error';

interface ErrorWithResponse {
  message?: string;
  response?: {
    status?: number;
    data?: {
      message?: string;
      error?: {
        code?: string;
        message?: string;
      };
    };
  };
}

export function getThesisHttpStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined;
  return (error as ErrorWithResponse).response?.status;
}

export function isForbiddenError(error: unknown): boolean {
  return getThesisHttpStatus(error) === 403;
}

export function isNotFoundError(error: unknown): boolean {
  return getThesisHttpStatus(error) === 404;
}

export function classifyThesisError(error: unknown): ThesisErrorKind {
  if (isForbiddenError(error)) return 'forbidden';
  if (isNotFoundError(error)) return 'not_found';
  return 'load_error';
}

export function getThesisErrorMessage(
  error: unknown,
  fallback = 'Không thể tải dữ liệu. Vui lòng kiểm tra kết nối và thử lại.',
): string {
  if (!error) return fallback;

  if (typeof error === 'object') {
    const errObj = error as ErrorWithResponse;
    const serverMessage =
      errObj.response?.data?.error?.message || errObj.response?.data?.message;
    if (serverMessage && serverMessage.trim()) {
      return serverMessage;
    }

    if (errObj.response?.status === 403) {
      return 'Bạn không có quyền truy cập dữ liệu này.';
    }
    if (errObj.response?.status === 404) {
      return 'Không tìm thấy dữ liệu yêu cầu.';
    }
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
