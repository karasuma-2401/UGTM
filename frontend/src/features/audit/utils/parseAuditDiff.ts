import type { AuditLogItem, AuditLogDiffData } from '../types/auditLog.types';

export const ACTION_LABELS: Record<string, string> = {
  'Identity.User.Created': 'Tạo tài khoản',
  'Identity.User.Updated': 'Cập nhật tài khoản',
  'Identity.User.StatusChanged': 'Đổi trạng thái tài khoản',
  'Identity.User.RolesChanged': 'Thay đổi vai trò người dùng',
  'Identity.Users.Imported': 'Nhập danh sách người dùng',
  'Identity.Role.Created': 'Tạo vai trò mới',
  'Identity.Role.PermissionsChanged': 'Cập nhật quyền vai trò',
  'Identity.Session.Revoked': 'Thu hồi phiên làm việc',
  'Identity.User.SessionsRevoked': 'Thu hồi toàn bộ phiên',
  'Identity.Authentication.Login': 'Đăng nhập hệ thống',
  'Identity.Session.Refreshed': 'Làm mới phiên làm việc',
  'Identity.Session.RefreshTokenReuseDetected': 'Cảnh báo dùng lại Token',
  'Identity.Session.LoggedOut': 'Đăng xuất',
  'Identity.Account.Activated': 'Kích hoạt tài khoản',
  'Identity.Account.ActivationRequested': 'Gửi email kích hoạt',
  'Identity.Account.PasswordChanged': 'Đổi mật khẩu',
  'Identity.Account.PasswordRecoveryRequested': 'Yêu cầu khôi phục mật khẩu',
  'Identity.Account.RecoveryOtpVerified': 'Xác thực OTP khôi phục',
  'Identity.Account.PasswordReset': 'Đặt lại mật khẩu',
  'AcademicPeriods.Batch.ConfigurationUpdated': 'Cấu hình đợt khóa luận',
  'AcademicPeriods.Score.ConfigurationUpdated': 'Cấu hình thang điểm',
  'ThesisManagement.RenameRequest.Approved': 'Duyệt đổi tên đề tài',
  'DefenseCouncil.CouncilMinute.UnlockGranted': 'Mở khóa biên bản hội đồng',
  'DefenseCouncil.Score.Adjusted': 'Điều chỉnh điểm hội đồng',
  'DefenseCouncil.Score.Excluded': 'Loại trừ điểm đánh giá',
};

export interface ActionGroupOption {
  group: string;
  items: { value: string; label: string }[];
}

export const ACTION_GROUPS: ActionGroupOption[] = [
  {
    group: 'Xác thực & Phiên làm việc',
    items: [
      { value: 'Identity.Authentication.Login', label: 'Đăng nhập hệ thống' },
      { value: 'Identity.Session.Refreshed', label: 'Làm mới phiên làm việc' },
      { value: 'Identity.Session.Revoked', label: 'Thu hồi phiên làm việc' },
      { value: 'Identity.User.SessionsRevoked', label: 'Thu hồi toàn bộ phiên' },
      { value: 'Identity.Session.RefreshTokenReuseDetected', label: 'Cảnh báo dùng lại Token' },
      { value: 'Identity.Session.LoggedOut', label: 'Đăng xuất' },
    ],
  },
  {
    group: 'Tài khoản người dùng',
    items: [
      { value: 'Identity.User.Created', label: 'Tạo tài khoản' },
      { value: 'Identity.User.Updated', label: 'Cập nhật tài khoản' },
      { value: 'Identity.User.StatusChanged', label: 'Đổi trạng thái tài khoản' },
      { value: 'Identity.User.RolesChanged', label: 'Thay đổi vai trò người dùng' },
      { value: 'Identity.Users.Imported', label: 'Nhập danh sách người dùng' },
      { value: 'Identity.Account.Activated', label: 'Kích hoạt tài khoản' },
      { value: 'Identity.Account.ActivationRequested', label: 'Gửi email kích hoạt' },
      { value: 'Identity.Account.PasswordChanged', label: 'Đổi mật khẩu' },
      { value: 'Identity.Account.PasswordReset', label: 'Đặt lại mật khẩu' },
    ],
  },
  {
    group: 'Phân quyền & Vai trò',
    items: [
      { value: 'Identity.Role.Created', label: 'Tạo vai trò mới' },
      { value: 'Identity.Role.PermissionsChanged', label: 'Cập nhật quyền vai trò' },
    ],
  },
  {
    group: 'Đợt khóa luận & Hội đồng',
    items: [
      { value: 'AcademicPeriods.Batch.ConfigurationUpdated', label: 'Cấu hình đợt khóa luận' },
      { value: 'AcademicPeriods.Score.ConfigurationUpdated', label: 'Cấu hình thang điểm' },
      { value: 'ThesisManagement.RenameRequest.Approved', label: 'Duyệt đổi tên đề tài' },
      { value: 'DefenseCouncil.CouncilMinute.UnlockGranted', label: 'Mở khóa biên bản hội đồng' },
      { value: 'DefenseCouncil.Score.Adjusted', label: 'Điều chỉnh điểm hội đồng' },
      { value: 'DefenseCouncil.Score.Excluded', label: 'Loại trừ điểm đánh giá' },
    ],
  },
];

export const STATUS_TEXT: Record<string, string> = {
  Active: 'Hoạt động',
  PendingActivation: 'Chờ kích hoạt',
  Suspended: 'Tạm khóa',
  Disabled: 'Vô hiệu hóa',
};

export const TARGET_TYPE_LABELS: Record<string, string> = {
  User: 'Tài khoản',
  Role: 'Vai trò',
  AuthenticationSession: 'Phiên làm việc',
  AcademicPeriod: 'Đợt khóa luận',
  ThesisTopic: 'Đề tài',
  CouncilMinute: 'Biên bản hội đồng',
  ThesisScore: 'Điểm số',
};

export function formatTargetType(targetType: string): string {
  return TARGET_TYPE_LABELS[targetType] || targetType;
}

export function formatAuditAction(action: string): string {
  return ACTION_LABELS[action] || action;
}

function vietnamTimeParts(isoString: string): Record<string, string> | null {
  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) return null;
  const formatter = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });
  return Object.fromEntries(formatter.formatToParts(date).map((part) => [part.type, part.value]));
}

export function formatAuditShortTime(isoString: string): string {
  const parts = vietnamTimeParts(isoString);
  return parts ? `${parts.day}/${parts.month} - ${parts.hour}:${parts.minute}` : isoString;
}

export function formatAuditFullTime(isoString: string): string {
  const parts = vietnamTimeParts(isoString);
  return parts
    ? `${parts.day}/${parts.month}/${parts.year} - ${parts.hour}:${parts.minute}:${parts.second} (UTC+7)`
    : isoString;
}

export function formatAuditRelativeTime(isoString: string): string {
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return '';
    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);
    if (diffSec < 60) return 'Vừa xong';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin} phút trước`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `${diffHours} giờ trước`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 30) return `${diffDays} ngày trước`;
    return '';
  } catch {
    return '';
  }
}

export function parseAuditDiff(item: AuditLogItem): AuditLogDiffData {
  const metadata = item.metadata || {};
  const reason =
    item.reasonCode ||
    (typeof metadata.Reason === 'string' ? metadata.Reason : undefined) ||
    (typeof metadata.reason === 'string' ? metadata.reason : undefined);
  const value = (...keys: string[]): unknown =>
    keys.map((key) => metadata[key]).find((entry) => entry !== undefined && entry !== null);
  const show = (entry: unknown): string =>
    entry === undefined || entry === null
      ? 'Không được ghi nhận'
      : typeof entry === 'object'
        ? JSON.stringify(entry)
        : String(entry);
  const pair = (title: string, before: unknown, after: unknown): AuditLogDiffData => ({
    fieldTitle: title,
    beforeValue: show(before),
    afterValue: show(after),
    reason,
    hasDiff: before !== undefined || after !== undefined,
  });
  if (item.action === 'Identity.User.StatusChanged') {
    const before = value('PreviousStatus', 'previousStatus');
    const after = value('NewStatus', 'newStatus');
    return pair(
      'Trạng thái tài khoản',
      typeof before === 'string' ? STATUS_TEXT[before] || before : before,
      typeof after === 'string' ? STATUS_TEXT[after] || after : after,
    );
  }
  if (
    item.action === 'Identity.Role.PermissionsChanged' ||
    item.action === 'Identity.User.RolesChanged'
  ) {
    const isRole = item.action === 'Identity.User.RolesChanged';
    const added = value(
      isRole ? 'AddedRoles' : 'AddedPermissionIds',
      isRole ? 'addedRoles' : 'addedPermissionIds',
    );
    const removed = value(
      isRole ? 'RemovedRoles' : 'RemovedPermissionIds',
      isRole ? 'removedRoles' : 'removedPermissionIds',
    );
    return pair(
      isRole ? 'Vai trò người dùng' : 'Quyền của vai trò',
      Array.isArray(removed)
        ? removed.length
          ? removed.join(', ')
          : 'Không có mục thu hồi'
        : undefined,
      Array.isArray(added)
        ? added.length
          ? added.join(', ')
          : 'Không có mục cấp thêm'
        : undefined,
    );
  }
  if (item.action === 'ThesisManagement.RenameRequest.Approved')
    return pair(
      'Tên đề tài',
      value('OldTitleVi', 'oldTitleVi', 'OldTopicName', 'oldTopicName'),
      value('NewTitleVi', 'newTitleVi', 'NewTopicName', 'newTopicName'),
    );
  if (item.action.startsWith('DefenseCouncil.Score.'))
    return pair('Điểm đánh giá', value('OldScore', 'oldScore'), value('NewScore', 'newScore'));
  return {
    fieldTitle: formatAuditAction(item.action),
    beforeValue: '',
    afterValue: '',
    reason,
    hasDiff: false,
  };
}
