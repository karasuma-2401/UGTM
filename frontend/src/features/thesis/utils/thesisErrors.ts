export type ThesisErrorKind = 'forbidden' | 'not_found' | 'load_error';

interface ErrorWithResponse {
  isAxiosError?: boolean;
  message?: string;
  response?: {
    status?: number;
    data?: {
      message?: string;
      detail?: string;
      title?: string;
      errors?: Record<string, unknown> | null;
      error?: {
        code?: string;
        message?: string;
        errors?: Record<string, unknown> | null;
      };
    };
  };
}

const RAW_AXIOS_MESSAGE_RE = /^(Request failed with status code \d+|Network Error)$/i;

function extractValidationErrors(errors: Record<string, unknown> | null | undefined): string | null {
  if (!errors || typeof errors !== 'object') return null;
  const messages: string[] = [];
  for (const value of Object.values(errors)) {
    if (Array.isArray(value)) {
      for (const entry of value) {
        if (typeof entry === 'string' && entry.trim()) {
          messages.push(entry.trim());
        }
      }
    } else if (typeof value === 'string' && value.trim()) {
      messages.push(value.trim());
    }
  }
  return messages.length > 0 ? messages.join(' ') : null;
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
    const data = errObj.response?.data;
    const serverMessage =
      data?.error?.message ||
      extractValidationErrors(data?.error?.errors) ||
      data?.detail ||
      data?.message ||
      extractValidationErrors(data?.errors) ||
      data?.title;
    if (serverMessage && serverMessage.trim()) {
      return serverMessage.trim();
    }

    if (errObj.response?.status === 403) {
      return 'Bạn không có quyền truy cập dữ liệu này.';
    }
    if (errObj.response?.status === 404) {
      return 'Không tìm thấy dữ liệu yêu cầu.';
    }
    if (errObj.isAxiosError && !errObj.response) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối rồi thử lại.';
    }
  }

  if (
    error instanceof Error &&
    error.message &&
    !RAW_AXIOS_MESSAGE_RE.test(error.message.trim())
  ) {
    return error.message;
  }

  return fallback;
}
