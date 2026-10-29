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

export const STATUS_TEXT: Record<string, string> = {
  Active: 'Hoạt động',
  PendingActivation: 'Chờ kích hoạt',
  Suspended: 'Tạm khóa',
  Disabled: 'Vô hiệu hóa',
};

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

  return {
    fieldTitle: `Trường dữ liệu: ${formatAuditAction(item.action)}`,
    beforeValue: item.targetType ? `${item.targetType} (ID: ${item.targetId})` : 'Dữ liệu trước thao tác',
    afterValue: item.outcome === 'Success' ? 'Thao tác hoàn tất thành công' : `Kết quả: ${item.outcome}`,
    reason,
    hasDiff: Object.keys(metadata).length > 0,
  };
}

