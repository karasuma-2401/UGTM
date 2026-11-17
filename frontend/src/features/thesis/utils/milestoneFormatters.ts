import type { ProgressReportStatus } from '../types/milestone.types';
import type { SubmissionLinkCategory } from '../types/syllabus.types';

export type ProgressReportDecision = 'Accepted' | 'NeedRevision';

export interface ProgressReportReviewRequest {
  decision: ProgressReportDecision;
  feedback?: string;
}

export function toReportStatus(status?: string | null): ProgressReportStatus {
  if (
    status === 'Submitted' ||
    status === 'Accepted' ||
    status === 'NeedRevision' ||
    status === 'NotSubmitted'
  ) {
    return status;
  }
  return 'NotSubmitted';
}

export type DeadlineUrgency = 'completed' | 'overdue' | 'soon' | 'normal';

export interface DeadlineInfo {
  urgency: DeadlineUrgency;
  label: string;
}

const pad = (value: number) => String(value).padStart(2, '0');

export function formatMilestoneDateTime(iso?: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function formatMilestoneDateShort(iso?: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

export function formatMilestoneTimeShort(iso?: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function formatLocalDateTimeInput(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function getDefaultFutureDeadlineInput(daysAhead = 7): string {
  const target = new Date();
  target.setDate(target.getDate() + daysAhead);
  target.setHours(23, 59, 0, 0);
  return formatLocalDateTimeInput(target);
}

export function getDeadlineInfo(
  deadlineUtc: string,
  reportStatus: ProgressReportStatus = 'NotSubmitted',
  nowMs = Date.now(),
): DeadlineInfo {
  const deadlineMs = new Date(deadlineUtc).getTime();
  if (Number.isNaN(deadlineMs)) {
    return { urgency: 'normal', label: 'Chưa xác định' };
  }

  if (reportStatus === 'Accepted') {
    return { urgency: 'completed', label: 'Đã hoàn thành' };
  }

  const diffMs = deadlineMs - nowMs;
  if (diffMs < 0 && (reportStatus === 'NotSubmitted' || reportStatus === 'NeedRevision')) {
    const overdueDays = Math.max(1, Math.ceil(Math.abs(diffMs) / 86_400_000));
    return { urgency: 'overdue', label: `Quá hạn ${overdueDays} ngày` };
  }

  if (reportStatus === 'Submitted') {
    return { urgency: 'normal', label: 'Đang chờ nhận xét' };
  }

  if (diffMs >= 0 && diffMs <= 48 * 3_600_000) {
    const hoursLeft = Math.max(1, Math.ceil(diffMs / 3_600_000));
    return {
      urgency: 'soon',
      label: hoursLeft <= 24 ? `Còn ${hoursLeft} giờ` : 'Sắp đến hạn',
    };
  }

  const daysLeft = Math.max(1, Math.ceil(diffMs / 86_400_000));
  return { urgency: 'normal', label: `Còn ${daysLeft} ngày` };
}

export function formatFileSize(bytes?: number | null): string {
  if (bytes == null || !Number.isFinite(bytes) || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(1)} KB`;
  const mb = kb / 1024;
  return `${mb.toFixed(2)} MB`;
}

export function formatDeadlineSummary(
  deadlineInput: string,
  nowMs = Date.now(),
): { validFuture: boolean; summary: string } | null {
  if (!deadlineInput) return null;
  const date = new Date(deadlineInput);
  if (Number.isNaN(date.getTime())) return null;

  const weekdayDate = new Intl.DateTimeFormat('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);

  const diffMs = date.getTime() - nowMs;
  if (diffMs <= 0) {
    return { validFuture: false, summary: `${weekdayDate} · Thời điểm này đã trôi qua` };
  }

  const totalHours = Math.round(diffMs / 3_600_000);
  if (totalHours < 24) {
    return { validFuture: true, summary: `${weekdayDate} · Còn khoảng ${Math.max(1, totalHours)} giờ` };
  }
  const nowDate = new Date(nowMs);
  const startOfNow = new Date(nowDate.getFullYear(), nowDate.getMonth(), nowDate.getDate()).getTime();
  const startOfTarget = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  const days = Math.max(1, Math.round((startOfTarget - startOfNow) / 86_400_000));
  return { validFuture: true, summary: `${weekdayDate} · Còn ${days} ngày` };
}

export function formatSubmissionDelta(
  deadlineUtc: string,
  submittedAtUtc: string,
): { isLate: boolean; label: string } {
  const deadlineMs = new Date(deadlineUtc).getTime();
  const submittedMs = new Date(submittedAtUtc).getTime();
  if (Number.isNaN(deadlineMs) || Number.isNaN(submittedMs)) {
    return { isLate: false, label: 'Chưa xác định' };
  }

  const diffMs = submittedMs - deadlineMs;
  const absHours = Math.max(1, Math.round(Math.abs(diffMs) / 3_600_000));
  const days = Math.floor(absHours / 24);
  const hours = absHours % 24;
  const duration =
    days > 0 ? (hours > 0 ? `${days} ngày ${hours} giờ` : `${days} ngày`) : `${absHours} giờ`;

  if (diffMs > 60_000) {
    return { isLate: true, label: `Trễ ${duration} so với hạn` };
  }
  if (Math.abs(diffMs) <= 3_600_000) {
    return { isLate: false, label: 'Đúng sát hạn' };
  }
  return { isLate: false, label: `Nộp sớm ${duration}` };
}

export function getSafeHttpUrl(raw?: string | null): string | null {
  if (!raw) return null;
  try {
    const parsed = new URL(raw.trim());
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
      return parsed.toString();
    }
    return null;
  } catch {
    return null;
  }
}

export interface FormattedSourceLink {
  hostLabel: string;
  displayText: string;
  isLocalPortalUrl: boolean;
}

export function formatGitSourceLink(rawUrl: string): FormattedSourceLink {
  try {
    const parsed = new URL(rawUrl);
    const host = parsed.hostname.toLowerCase();
    const isLocalHost =
      host === 'localhost' ||
      host === '127.0.0.1' ||
      (typeof window !== 'undefined' && parsed.origin === window.location.origin);

    const path = `${parsed.pathname}${parsed.search}`.replace(/\/+$/, '');
    const shortPath = path.length > 42 ? `${path.slice(0, 39)}…` : path;

    if (host.includes('github.com')) {
      return {
        hostLabel: 'GitHub',
        displayText: `github.com${shortPath}`,
        isLocalPortalUrl: false,
      };
    }
    if (host.includes('gitlab.com')) {
      return {
        hostLabel: 'GitLab',
        displayText: `gitlab.com${shortPath}`,
        isLocalPortalUrl: false,
      };
    }
    if (host.includes('drive.google.com') || host.includes('docs.google.com')) {
      return {
        hostLabel: 'Google Drive',
        displayText: `${host}${shortPath}`,
        isLocalPortalUrl: false,
      };
    }
    if (isLocalHost) {
      return {
        hostLabel: 'Liên kết đính kèm',
        displayText: `${parsed.host}${shortPath}`,
        isLocalPortalUrl: true,
      };
    }
    return {
      hostLabel: 'Liên kết ngoài',
      displayText: `${parsed.host}${shortPath}`,
      isLocalPortalUrl: false,
    };
  } catch {
    return {
      hostLabel: 'Liên kết sản phẩm',
      displayText: rawUrl,
      isLocalPortalUrl: false,
    };
  }
}

export function formatFileSizeDelta(
  currentBytes?: number | null,
  previousBytes?: number | null,
): string | null {
  if (
    currentBytes == null ||
    previousBytes == null ||
    !Number.isFinite(currentBytes) ||
    !Number.isFinite(previousBytes)
  ) {
    return null;
  }
  const diff = currentBytes - previousBytes;
  if (Math.abs(diff) < 1024) return 'Dung lượng tương đương bản trước';
  const sign = diff > 0 ? '+' : '−';
  const absDiff = Math.abs(diff);
  const formatted = absDiff < 1024 * 1024
    ? `${(absDiff / 1024).toFixed(0)} KB`
    : `${(absDiff / (1024 * 1024)).toFixed(2)} MB`;
  return `${sign}${formatted} so với bản trước`;
}

export const SUBMISSION_LINK_CATEGORY_LABELS: Record<SubmissionLinkCategory, string> = {
  SourceCode: 'Mã nguồn',
  Demo: 'Demo / Video',
  Documentation: 'Tài liệu / Thiết kế',
  Other: 'Khác',
};

export const SUBMISSION_LINK_CATEGORIES: SubmissionLinkCategory[] = [
  'SourceCode',
  'Demo',
  'Documentation',
  'Other',
];

export function detectLinkCategory(rawUrl: string): SubmissionLinkCategory | null {
  try {
    const host = new URL(rawUrl.trim()).hostname.toLowerCase();
    if (
      host.includes('github.com') ||
      host.includes('gitlab.com') ||
      host.includes('bitbucket.org') ||
      host.includes('codeberg.org')
    ) {
      return 'SourceCode';
    }
    if (
      host.includes('youtu.be') ||
      host.includes('youtube.com') ||
      host.includes('loom.com') ||
      host.includes('vimeo.com') ||
      host.includes('vercel.app') ||
      host.includes('netlify.app')
    ) {
      return 'Demo';
    }
    if (
      host.includes('figma.com') ||
      host.includes('docs.google.com') ||
      host.includes('drive.google.com') ||
      host.includes('notion.so') ||
      host.includes('overleaf.com') ||
      host.includes('canva.com')
    ) {
      return 'Documentation';
    }
    return null;
  } catch {
    return null;
  }
}
