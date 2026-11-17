import type { AxiosProgressEvent } from 'axios';

export const REPORT_MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
export const REPORT_ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx'] as const;

export const IMAGE_MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024;
export const IMAGE_MAX_COUNT = 5;
export const IMAGE_ALLOWED_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp'] as const;

export const PRODUCT_LINK_MAX_COUNT = 5;

export function buildUploadProgressHandler(onUploadProgress?: (percent: number) => void) {
  if (!onUploadProgress) return undefined;
  return (event: AxiosProgressEvent) => {
    if (!event.total || event.total <= 0) return;
    const pct = Math.min(99, Math.max(1, Math.round((event.loaded * 100) / event.total)));
    onUploadProgress(pct);
  };
}

export function validateReportFile(file: File): string | null {
  const lower = file.name.toLowerCase();
  const hasValidExt = REPORT_ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext));
  if (!hasValidExt) {
    return 'Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.';
  }
  if (file.size <= 0) {
    return 'Tệp báo cáo rỗng, vui lòng kiểm tra lại.';
  }
  if (file.size > REPORT_MAX_FILE_SIZE_BYTES) {
    return 'Dung lượng tệp báo cáo không được vượt quá 20 MB.';
  }
  return null;
}

export function validateImageAttachmentFile(file: File): string | null {
  const lower = file.name.toLowerCase();
  const hasValidExt = IMAGE_ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext));
  const hasValidMime =
    file.type === '' ||
    file.type === 'image/png' ||
    file.type === 'image/jpeg' ||
    file.type === 'image/webp';
  if (!hasValidExt || !hasValidMime) {
    return `"${file.name}" không đúng định dạng ảnh (PNG, JPG, JPEG hoặc WEBP).`;
  }
  if (file.size <= 0) {
    return `"${file.name}" là tệp rỗng.`;
  }
  if (file.size > IMAGE_MAX_FILE_SIZE_BYTES) {
    return `"${file.name}" vượt quá giới hạn 5 MB mỗi ảnh.`;
  }
  return null;
}
