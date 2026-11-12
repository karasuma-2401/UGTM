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

export function formatAuditShortTime(isoString: string): string {
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return isoString;

    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');

    return `${day}/${month} - ${hours}:${minutes}`;
  } catch {
    return isoString;
  }
}

export function formatAuditFullTime(isoString: string): string {
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return isoString;

    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');

    return `${day}/${month}/${year} - ${hours}:${minutes}:${seconds}`;
  } catch {
    return isoString;
  }
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
  const reason = item.reasonCode || metadata.Reason || metadata.reason || 'Thao tác hợp lệ được ghi nhận.';

  if (item.action === 'Identity.User.StatusChanged') {
    const prev = metadata.PreviousStatus || metadata.previousStatus;
    const next = metadata.NewStatus || metadata.newStatus;
    return {
      fieldTitle: 'Trường dữ liệu: Trạng thái tài khoản',
      beforeValue: prev ? (STATUS_TEXT[prev] || prev) : 'Chưa xác định',
      afterValue: next ? (STATUS_TEXT[next] || next) : 'Chưa xác định',
      reason,
      hasDiff: true,
    };
  }

  if (item.action === 'Identity.Role.PermissionsChanged') {
    const added = metadata.AddedPermissionIds || metadata.addedPermissionIds || [];
    const removed = metadata.RemovedPermissionIds || metadata.removedPermissionIds || [];
    const current = metadata.CurrentPermissionIds || metadata.currentPermissionIds || [];

    return {
      fieldTitle: 'Trường dữ liệu: Quyền hạn vai trò',
      beforeValue: removed.length > 0 ? `Đã gỡ: ${removed.join(', ')}` : 'Không có quyền bị gỡ',
      afterValue: added.length > 0 ? `Cấp thêm: ${added.join(', ')} (Hiện có ${current.length} quyền)` : `Hiện có ${current.length} quyền`,
      reason,
      hasDiff: true,
    };
  }

  if (item.action === 'Identity.User.RolesChanged') {
    const added = metadata.AddedRoles || metadata.addedRoles || [];
    const removed = metadata.RemovedRoles || metadata.removedRoles || [];

    return {
      fieldTitle: 'Trường dữ liệu: Phân quyền vai trò người dùng',
      beforeValue: removed.length > 0 ? `Bỏ vai trò: ${removed.join(', ')}` : 'Không có vai trò bị gỡ',
      afterValue: added.length > 0 ? `Gán thêm: ${added.join(', ')}` : 'Không có vai trò gán thêm',
      reason,
      hasDiff: true,
    };
  }

  if (item.action === 'Identity.User.Updated') {
    const changed = metadata.ChangedFields || metadata.changedFields || [];
    return {
      fieldTitle: 'Trường dữ liệu: Thông tin tài khoản',
      beforeValue: 'Giá trị thông tin cũ trước khi sửa',
      afterValue: changed.length > 0 ? `Đã cập nhật các trường: ${changed.join(', ')}` : 'Đã cập nhật thông tin',
      reason,
      hasDiff: true,
    };
  }

  if (item.action === 'Identity.User.Created') {
    const roles = metadata.Roles || metadata.roles || [];
    const status = metadata.Status || metadata.status || 'Active';
    return {
      fieldTitle: 'Trường dữ liệu: Hồ sơ người dùng mới',
      beforeValue: 'Chưa có trên hệ thống',
      afterValue: `Đã tạo (Trạng thái: ${STATUS_TEXT[status] || status}, Vai trò: ${roles.join(', ') || 'Mặc định'})`,
      reason,
      hasDiff: true,
    };
  }

  if (item.action.startsWith('Identity.Authentication.')) {
    return {
      fieldTitle: 'Trường dữ liệu: Phiên xác thực người dùng',
      beforeValue: 'Chưa xác thực',
      afterValue: item.outcome === 'Success' ? 'Đăng nhập thành công' : `Thất bại (${item.reasonCode || 'Sai thông tin'})`,
      reason: item.reasonCode ? `Mã lý do: ${item.reasonCode}` : reason,
      hasDiff: true,
    };
  }

  if (item.action === 'ThesisManagement.RenameRequest.Approved') {
    return {
      fieldTitle: 'Trường dữ liệu: Tên đề tài khóa luận',
      beforeValue: metadata.OldTopicName || metadata.oldTopicName || 'Tên đề tài cũ',
      afterValue: metadata.NewTopicName || metadata.newTopicName || 'Tên đề tài mới',
      reason,
      hasDiff: true,
    };
  }

  if (item.action.startsWith('DefenseCouncil.Score.')) {
    return {
      fieldTitle: 'Trường dữ liệu: Điểm đánh giá hội đồng',
      beforeValue: metadata.OldScore !== undefined ? String(metadata.OldScore) : 'Điểm trước điều chỉnh',
      afterValue: metadata.NewScore !== undefined ? String(metadata.NewScore) : 'Điểm sau điều chỉnh',
      reason,
      hasDiff: true,
    };
  }

  const beforeKey = Object.keys(metadata).find((k) => /before|old|prev/i.test(k));
  const afterKey = Object.keys(metadata).find((k) => /after|new|next/i.test(k));

  if (beforeKey && afterKey) {
    return {
      fieldTitle: `Trường dữ liệu: ${formatAuditAction(item.action)}`,
      beforeValue: String(metadata[beforeKey]),
      afterValue: String(metadata[afterKey]),
      reason,
      hasDiff: true,
    };
  }

  // Sự kiện vận hành (operational event), không phải thay đổi trường
  const hasMetadata = Object.keys(metadata).length > 0;
  return {
    fieldTitle: `Hành động: ${formatAuditAction(item.action)}`,
    beforeValue: item.targetType ? `${formatTargetType(item.targetType)} (ID: ${item.targetId})` : 'Dữ liệu trước thao tác',
    afterValue: item.outcome === 'Success' ? 'Thao tác hoàn tất thành công' : `Kết quả: ${item.outcome}`,
    reason,
    hasDiff: hasMetadata && (item.action.includes('Changed') || item.action.includes('Updated')),
  };
}
