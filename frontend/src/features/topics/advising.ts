import type { GroupScope } from './types/advising.types';

export const GROUP_SCOPE_LABELS: Record<GroupScope, string> = {
  active: 'Đang thực hiện',
  all: 'Tất cả nhóm',
  completed: 'Đã hoàn thành',
  stopped: 'Đã dừng',
};
export const groupLabel = (id: string) => `Nhóm ${id.slice(0, 8).toUpperCase()}`;
export const formatAdvisingDate = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value));

export function confirmationLabel(
  status: string,
  expiresAt?: string | null,
  invitationExpired = false,
): string {
  if (status === 'Confirmed') return 'Đã xác nhận';
  if (status === 'Expired' || invitationExpired) return 'Lời mời đã hết hạn';
  if (status === 'Pending') {
    return expiresAt ? 'Chờ xác nhận' : 'Đang gửi lời mời';
  }
  return 'Chưa rõ trạng thái';
}
