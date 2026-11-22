import axios from 'axios';
import type { AuditLogItem } from '@/features/audit/types/auditLog.types';
import type {
  UserActivityDay,
  UserPermissionDetail,
  UserProfile,
} from '@/features/auth/types/auth.types';
import { permissionModuleLabel } from '@/features/identity/utils/permissionLabels';

const VIETNAM_PHONE_REGEX = /^(0|\+84|84)\d{9}$/;

export function isValidVietnamPhone(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) return true;
  return VIETNAM_PHONE_REGEX.test(trimmed);
}

export function normalizePhoneValue(value: string | null | undefined): string {
  return (value ?? '').trim();
}

export interface ParsedDeviceInfo {
  browser: string;
  os: string;
  deviceType: 'desktop' | 'mobile' | 'tablet' | 'unknown';
  title: string;
  subtitle: string;
}

export function parseUserAgent(ua: string | null | undefined): ParsedDeviceInfo {
  if (!ua || !ua.trim()) {
    return {
      browser: 'Trình duyệt không xác định',
      os: 'Thiết bị chưa rõ',
      deviceType: 'unknown',
      title: 'Phiên trình duyệt web',
      subtitle: 'Không ghi nhận thông tin User-Agent',
    };
  }

  const raw = ua.trim();

  let os = 'Hệ điều hành khác';
  let deviceType: ParsedDeviceInfo['deviceType'] = 'desktop';

  if (/iPad|Tablet/i.test(raw)) {
    os = 'iPadOS / Tablet';
    deviceType = 'tablet';
  } else if (/iPhone/i.test(raw)) {
    os = 'iOS (iPhone)';
    deviceType = 'mobile';
  } else if (/Android/i.test(raw)) {
    os = 'Android';
    deviceType = /Mobile/i.test(raw) ? 'mobile' : 'tablet';
  } else if (/Windows NT 10\.0|Windows/i.test(raw)) {
    os = 'Windows';
    deviceType = 'desktop';
  } else if (/Mac OS X|Macintosh/i.test(raw)) {
    os = 'macOS';
    deviceType = 'desktop';
  } else if (/Linux/i.test(raw)) {
    os = 'Linux';
    deviceType = 'desktop';
  }

  let browser = 'Trình duyệt';
  if (/Edg\/(\d+)/i.test(raw)) {
    browser = `Microsoft Edge ${RegExp.$1}`;
  } else if (/OPR\/(\d+)/i.test(raw) || /Opera/i.test(raw)) {
    browser = 'Opera';
  } else if (/Firefox\/(\d+)/i.test(raw)) {
    browser = `Mozilla Firefox ${RegExp.$1}`;
  } else if (/Chrome\/(\d+)/i.test(raw) && !/Chromium/i.test(raw)) {
    browser = `Google Chrome ${RegExp.$1}`;
  } else if (/Safari\/(\d+)/i.test(raw) && /Version\/(\d+)/i.test(raw)) {
    browser = `Safari ${RegExp.$1}`;
  } else if (/curl|PostmanRuntime|HttpClient/i.test(raw)) {
    browser = 'API Client';
    os = 'Tự động / CLI';
  }

  return {
    browser,
    os,
    deviceType,
    title: `${browser} trên ${os}`,
    subtitle: raw,
  };
}

export function formatVietnamDateTime(
  iso: string | null | undefined,
  fallback = 'Chưa ghi nhận',
): string {
  if (!iso) return fallback;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return fallback;
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    dateStyle: 'short',
    timeStyle: 'short',
    hourCycle: 'h23',
  }).format(date);
}

export function formatVietnamDateOnly(
  iso: string | null | undefined,
  fallback = 'Chưa ghi nhận',
): string {
  if (!iso) return fallback;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return fallback;
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

export interface SessionWindowProgress {
  percent: number;
  remainingText: string;
  isExpiringSoon: boolean;
}

export function computeSessionRemaining(
  expiresAtIso: string,
  maxWindowDays: number,
): SessionWindowProgress {
  const expiresAt = new Date(expiresAtIso).getTime();
  const now = Date.now();
  if (Number.isNaN(expiresAt)) {
    return { percent: 0, remainingText: 'Không xác định', isExpiringSoon: false };
  }

  const diffMs = expiresAt - now;
  if (diffMs <= 0) {
    return { percent: 0, remainingText: 'Đã hết hạn', isExpiringSoon: true };
  }

  const totalMs = maxWindowDays * 24 * 60 * 60 * 1000;
  const percent = Math.min(100, Math.max(4, Math.round((diffMs / totalMs) * 100)));

  const totalMinutes = Math.floor(diffMs / 60_000);
  const days = Math.floor(totalMinutes / (60 * 24));
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
  const minutes = totalMinutes % 60;

  let remainingText = '';
  if (days > 0) {
    remainingText = hours > 0 ? `Còn ${days} ngày ${hours} giờ` : `Còn ${days} ngày`;
  } else if (hours > 0) {
    remainingText = `Còn ${hours} giờ ${minutes} phút`;
  } else {
    remainingText = `Còn ${Math.max(1, minutes)} phút`;
  }

  return {
    percent,
    remainingText,
    isExpiringSoon: diffMs < 24 * 60 * 60 * 1000,
  };
}

export interface SecurityCheckItem {
  id: string;
  label: string;
  passed: boolean;
  detail: string;
}

export interface SecurityHealthSummary {
  passedCount: number;
  totalCount: number;
  percent: number;
  tone: 'success' | 'warning' | 'error';
  statusLabel: string;
  checks: SecurityCheckItem[];
}

export function computeSecurityHealth(
  user: UserProfile,
  activeSessionCount: number | null,
): SecurityHealthSummary {
  const mustChange = Boolean(user.mustChangePassword);
  const hasPhone = Boolean(user.phoneNumber && user.phoneNumber.trim().length > 0);
  const isActive = (user.status ?? 'Active') === 'Active';
  const hasPasswordRecord = Boolean(user.passwordChangedAt);

  const checks: SecurityCheckItem[] = [
    {
      id: 'password-policy',
      label: 'Trạng thái mật khẩu',
      passed: !mustChange,
      detail: mustChange
        ? 'Cần đổi mật khẩu tạm'
        : hasPasswordRecord
          ? `Đổi lúc ${formatVietnamDateOnly(user.passwordChangedAt)}`
          : 'Đạt chuẩn bảo mật',
    },
    {
      id: 'phone',
      label: 'Số điện thoại liên hệ',
      passed: hasPhone,
      detail: hasPhone ? user.phoneNumber!.trim() : 'Chưa bổ sung',
    },
    {
      id: 'account-active',
      label: 'Xác minh & kích hoạt',
      passed: isActive,
      detail: isActive ? 'Đã kích hoạt' : 'Đang hạn chế',
    },
    {
      id: 'sessions',
      label: 'Phiên đăng nhập',
      passed: activeSessionCount === null ? true : activeSessionCount <= 5,
      detail:
        activeSessionCount === null
          ? 'Đang kiểm tra'
          : activeSessionCount === 0
            ? 'Không có phiên'
            : `${activeSessionCount} thiết bị`,
    },
  ];

  const passedCount = checks.filter((item) => item.passed).length;
  const totalCount = checks.length;
  const percent = Math.round((passedCount / totalCount) * 100);

  if (mustChange || !isActive) {
    return {
      passedCount,
      totalCount,
      percent,
      tone: 'error',
      statusLabel: mustChange ? 'Cần đổi mật khẩu' : 'Cần kiểm tra',
      checks,
    };
  }

  if (passedCount < totalCount) {
    return {
      passedCount,
      totalCount,
      percent,
      tone: 'warning',
      statusLabel: 'Khá tốt',
      checks,
    };
  }

  return {
    passedCount,
    totalCount,
    percent,
    tone: 'success',
    statusLabel: 'Tốt',
    checks,
  };
}

export interface GroupedUserPermissions {
  moduleKey: string;
  moduleLabel: string;
  items: UserPermissionDetail[];
}

function inferModuleFromCode(code: string): string {
  const prefix = code.split('.')[0]?.toLowerCase() ?? 'general';
  if (prefix === 'academic') return 'AcademicPeriods';
  if (prefix === 'topics') return 'TopicCatalog';
  if (prefix === 'thesis') return 'ThesisManagement';
  if (prefix === 'councils') return 'DefenseCouncil';
  if (prefix === 'identity') return 'Identity';
  if (prefix === 'notifications') return 'Notifications';
  return 'General';
}

export function groupUserPermissions(user: UserProfile): GroupedUserPermissions[] {
  const details: UserPermissionDetail[] =
    user.permissionDetails && user.permissionDetails.length > 0
      ? user.permissionDetails
      : (user.permissions ?? []).map((code) => ({
          permissionCode: code,
          permissionName: code,
          module: inferModuleFromCode(code),
        }));

  const map = new Map<string, UserPermissionDetail[]>();
  for (const item of details) {
    const key = item.module?.trim() || inferModuleFromCode(item.permissionCode);
    const list = map.get(key) ?? [];
    list.push(item);
    map.set(key, list);
  }

  return Array.from(map.entries())
    .map(([moduleKey, items]) => ({
      moduleKey,
      moduleLabel: permissionModuleLabel(moduleKey),
      items: items.sort((a, b) => a.permissionCode.localeCompare(b.permissionCode)),
    }))
    .sort((a, b) => a.moduleLabel.localeCompare(b.moduleLabel, 'vi'));
}

export const ACCOUNT_STATUS_DISPLAY: Record<
  string,
  { label: string; tone: 'success' | 'warning' | 'orange' | 'error' }
> = {
  Active: { label: 'Đang hoạt động', tone: 'success' },
  PendingActivation: { label: 'Chờ kích hoạt', tone: 'warning' },
  Suspended: { label: 'Tạm ngưng', tone: 'orange' },
  Disabled: { label: 'Vô hiệu hóa', tone: 'error' },
};

const ERROR_MESSAGES_BY_CODE: Record<string, string> = {
  'Identity.CurrentPasswordIncorrect': 'Mật khẩu hiện tại không chính xác. Vui lòng kiểm tra lại.',
  'Identity.WeakPassword':
    'Mật khẩu mới chưa đáp ứng chính sách bảo mật (12–128 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt, không chứa khoảng trắng).',
  'Identity.PasswordMustDifferFromCurrent': 'Mật khẩu mới phải khác với mật khẩu hiện tại.',
  'Identity.InvalidPhoneNumber':
    'Số điện thoại không hợp lệ (chỉ chấp nhận 10 chữ số, ví dụ 0901234567 hoặc +84901234567).',
  'Identity.SessionNotFound': 'Phiên đăng nhập không tồn tại hoặc đã bị thu hồi trước đó.',
  'Identity.RateLimitExceeded': 'Bạn thao tác quá nhanh. Vui lòng chờ ít phút rồi thử lại.',
  'Identity.PasswordChangeRequired':
    'Tài khoản đang bị yêu cầu đổi mật khẩu trước khi thực hiện thao tác khác.',
};

export function getProfileErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const apiErr = (error.response?.data as { error?: { code?: string; message?: string } } | undefined)
      ?.error;
    if (apiErr?.code && ERROR_MESSAGES_BY_CODE[apiErr.code]) {
      return ERROR_MESSAGES_BY_CODE[apiErr.code];
    }
    if (apiErr?.message) return apiErr.message;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export interface HeatmapCell {
  date: string;
  label: string;
  totalCount: number;
  successCount: number;
  alertCount: number;
  level: 0 | 1 | 2 | 3;
  inRange: boolean;
}

export interface HeatmapWeekColumn {
  weekStart: string;
  monthLabel: string | null;
  cells: HeatmapCell[];
}

function formatUtcDateKey(date: Date): string {
  const y = date.getUTCFullYear();
  const m = String(date.getUTCMonth() + 1).padStart(2, '0');
  const d = String(date.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function formatDisplayDateFromKey(dateKey: string): string {
  const [y, m, d] = dateKey.split('-');
  return `${d}/${m}/${y}`;
}

/**
 * Builds a Monday-to-Sunday 16-week activity grid aligned to Vietnam time (UTC+7).
 */
export function buildContributionWeeks(
  daysData: UserActivityDay[],
  weeksCount = 16,
  referenceNow: Date = new Date(),
): HeatmapWeekColumn[] {
  const dataMap = new Map<string, UserActivityDay>();
  for (const item of daysData) {
    dataMap.set(item.date, item);
  }

  // Shift reference timestamp by +7h so UTC getters yield local Vietnam calendar dates.
  const vnNow = new Date(referenceNow.getTime() + 7 * 60 * 60 * 1000);
  const todayUtc = new Date(
    Date.UTC(vnNow.getUTCFullYear(), vnNow.getUTCMonth(), vnNow.getUTCDate()),
  );
  const totalDays = weeksCount * 7;
  const rangeStart = new Date(todayUtc.getTime() - (totalDays - 1) * 86_400_000);

  // Align grid start backward to Monday (getUTCDay(): 0 = Sun .. 6 = Sat).
  const startDow = (rangeStart.getUTCDay() + 6) % 7;
  const gridStart = new Date(rangeStart.getTime() - startDow * 86_400_000);

  const endDow = (todayUtc.getUTCDay() + 6) % 7;
  const gridEnd = new Date(todayUtc.getTime() + (6 - endDow) * 86_400_000);

  const weeks: HeatmapWeekColumn[] = [];
  let lastMonthLabeled = -1;

  for (let cursor = gridStart.getTime(); cursor <= gridEnd.getTime(); cursor += 7 * 86_400_000) {
    const weekDate = new Date(cursor);
    const cells: HeatmapCell[] = [];
    let monthLabel: string | null = null;

    for (let offset = 0; offset < 7; offset++) {
      const cellDate = new Date(cursor + offset * 86_400_000);
      const dateKey = formatUtcDateKey(cellDate);
      const inRange =
        cellDate.getTime() >= rangeStart.getTime() && cellDate.getTime() <= todayUtc.getTime();
      const entry = dataMap.get(dateKey);
      const totalCount = entry?.totalCount ?? 0;
      const successCount = entry?.successCount ?? 0;
      const alertCount = entry?.alertCount ?? 0;
      const level: 0 | 1 | 2 | 3 =
        totalCount === 0 ? 0 : totalCount === 1 ? 1 : totalCount <= 3 ? 2 : 3;

      if (inRange && monthLabel === null && cellDate.getUTCMonth() !== lastMonthLabeled) {
        if (cellDate.getUTCDate() <= 7 || weeks.length === 0) {
          lastMonthLabeled = cellDate.getUTCMonth();
          monthLabel = `Thg ${lastMonthLabeled + 1}`;
        }
      }

      cells.push({
        date: dateKey,
        label: formatDisplayDateFromKey(dateKey),
        totalCount,
        successCount,
        alertCount,
        level,
        inRange,
      });
    }

    weeks.push({
      weekStart: formatUtcDateKey(weekDate),
      monthLabel,
      cells,
    });
  }

  return weeks;
}

export interface ActivityActionCluster {
  id: string;
  action: string;
  items: AuditLogItem[];
  alertCount: number;
  latestTimestamp: string;
}

export interface ActivityMonthGroup {
  monthKey: string;
  monthTitle: string;
  yearTitle: string;
  clusters: ActivityActionCluster[];
}

export function groupActivityByMonthAndAction(logs: AuditLogItem[]): ActivityMonthGroup[] {
  const monthMap = new Map<
    string,
    {
      monthTitle: string;
      yearTitle: string;
      clusterMap: Map<string, ActivityActionCluster>;
    }
  >();

  for (const log of logs) {
    const parsed = new Date(log.timestamp);
    if (Number.isNaN(parsed.getTime())) continue;

    // Convert UTC timestamp to Vietnam local date parts (UTC+7).
    const vn = new Date(parsed.getTime() + 7 * 60 * 60 * 1000);
    const year = vn.getUTCFullYear();
    const month = vn.getUTCMonth() + 1;
    const monthKey = `${year}-${String(month).padStart(2, '0')}`;

    let monthBucket = monthMap.get(monthKey);
    if (!monthBucket) {
      monthBucket = {
        monthTitle: `Tháng ${month}`,
        yearTitle: String(year),
        clusterMap: new Map(),
      };
      monthMap.set(monthKey, monthBucket);
    }

    const clusterId = `${monthKey}::${log.action}`;
    let cluster = monthBucket.clusterMap.get(clusterId);
    const isAlert = log.outcome === 'Denied' || log.outcome === 'Failed';
    if (!cluster) {
      cluster = {
        id: clusterId,
        action: log.action,
        items: [log],
        alertCount: isAlert ? 1 : 0,
        latestTimestamp: log.timestamp,
      };
      monthBucket.clusterMap.set(clusterId, cluster);
    } else {
      cluster.items.push(log);
      if (isAlert) cluster.alertCount += 1;
    }
  }

  return Array.from(monthMap.entries()).map(([monthKey, bucket]) => ({
    monthKey,
    monthTitle: bucket.monthTitle,
    yearTitle: bucket.yearTitle,
    clusters: Array.from(bucket.clusterMap.values()),
  }));
}
