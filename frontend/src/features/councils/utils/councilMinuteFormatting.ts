import axios from 'axios';
import type { StatusTone } from '@/components/ui/status-badge';
import { formatScoreReason } from './scoreSummaryFormatting';

const COUNCIL_MINUTE_ERROR_MESSAGES: Record<string, string> = {
  'Council.Forbidden': 'Bạn không có quyền truy cập biên bản của hội đồng này.',
  'Council.AssignmentNotFound': 'Không tìm thấy phân công nhóm đề tài trong hội đồng.',
  'Council.OnlySecretaryCanSaveMinute':
    'Chỉ Thư ký của hội đồng này (còn vai trò Giảng viên), Phòng Đào tạo hoặc Quản trị viên mới được lưu biên bản.',
  'Council.OnlySecretaryCanLockMinute':
    'Chỉ Thư ký của hội đồng này, Phòng Đào tạo hoặc Quản trị viên mới có quyền khóa biên bản.',
  'Council.MinuteLocked': 'Biên bản hội đồng đã bị khóa, không thể lưu hoặc chỉnh sửa.',
  'Council.MinuteAlreadyLocked': 'Biên bản hội đồng đã ở trạng thái khóa trước đó.',
  'Council.MinuteNotLocked': 'Biên bản chưa khóa hoặc đã được mở khóa đặc biệt trước đó.',
  'Council.MinuteNotReadyToLock':
    'Chưa đủ điều kiện khóa biên bản: cần đủ điểm hợp lệ, cấu hình điểm Active và đã xử lý sai biệt (nếu có).',
  'Council.NotActiveForScoring': 'Hội đồng chưa được Phòng Đào tạo phê duyệt hoạt động (Active).',
  'Council.InvalidSecretaryMarking': 'Lựa chọn đánh dấu của Thư ký không hợp lệ.',
  'Council.TargetComponentRequiredForRejection':
    'Vui lòng chọn đầu điểm cụ thể cần loại khi đánh dấu không công nhận theo biên bản.',
  'Council.InvalidTargetComponent':
    'Đầu điểm được chọn để loại không hợp lệ, chưa có điểm hoặc thuộc vai trò chỉ tham dự.',
  'Council.ExclusionReasonRequired':
    'Vui lòng nhập lý do nội bộ riêng (từ 10 đến 1000 ký tự) khi loại đầu điểm hoặc thay đổi cờ loại điểm.',
  'Council.PasswordRequired': 'Vui lòng nhập mật khẩu hiện tại để xác thực khóa biên bản.',
  'Council.ReauthenticationFailed': 'Mật khẩu xác thực không đúng. Vui lòng kiểm tra và thử lại.',
  'Council.ReauthenticationLocked':
    'Tài khoản tạm thời bị khóa xác thực lại do nhập sai mật khẩu quá số lần cho phép.',
  'Council.UnlockForbidden':
    'Chỉ Lãnh đạo Khoa có quyền Mở khóa đặc biệt hoặc Quản trị viên mới được thực hiện thao tác này.',
  'Council.OverrideReasonRequired':
    'Vui lòng nhập lý do mở khóa đặc biệt từ 10 đến 500 ký tự.',
  'Council.ScoresAlreadyPublished':
    'Bảng điểm của nhóm đã được công bố cho sinh viên, không thể thay đổi trạng thái biên bản.',
};

export interface LockStatusPresentation {
  label: string;
  tone: StatusTone;
  description: string;
}

export function getMinuteLockStatusMeta(lockStatus: string | null | undefined): LockStatusPresentation {
  switch (lockStatus) {
    case 'Locked':
      return {
        label: 'Đã khóa',
        tone: 'success',
        description: 'Toàn bộ bảng điểm đã đóng băng ở chế độ chỉ đọc để phục vụ lưu chiểu và tổng kết.',
      };
    case 'UnlockedByOverride':
      return {
        label: 'Mở khóa đặc biệt',
        tone: 'error',
        description:
          'Lãnh đạo Khoa đã cấp quyền mở khóa để điều chỉnh sai sót hoặc giải quyết khiếu nại; cần lưu & khóa lại sau khi xong.',
      };
    case 'AllScoresEntered':
      return {
        label: 'Đã đủ điểm',
        tone: 'info',
        description: 'Các thành viên bắt buộc đã hoàn tất nhập điểm, sẵn sàng rà soát và khóa biên bản.',
      };
    default:
      return {
        label: 'Chưa khóa',
        tone: 'warning',
        description: 'Biên bản đang mở để Thư ký ghi nhận góp ý, đối soát sai biệt và chuẩn bị khóa.',
      };
  }
}

export function getComponentFullLabel(componentCode: string, fallbackLabel: string): string {
  switch (componentCode) {
    case 'ADVISOR':
      return 'Giảng viên hướng dẫn';
    case 'REVIEWER':
      return 'Giảng viên phản biện';
    case 'COUNCIL_CHAIRMAN':
      return 'Chủ tịch Hội đồng';
    case 'COUNCIL_SECRETARY':
      return 'Thư ký Hội đồng';
    case 'COUNCIL_MEMBER':
      return 'Ủy viên Hội đồng';
    default:
      return fallbackLabel;
  }
}

export function getComponentShortLabel(componentCode: string, fallbackLabel: string): string {
  switch (componentCode) {
    case 'ADVISOR':
      return 'GVHD';
    case 'REVIEWER':
      return 'GVPB';
    case 'COUNCIL_CHAIRMAN':
      return 'Chủ tịch';
    case 'COUNCIL_SECRETARY':
      return 'Thư ký';
    case 'COUNCIL_MEMBER':
      return 'Ủy viên';
    default:
      return fallbackLabel;
  }
}

export function formatDefenseDateVi(dateStr: string | null | undefined): string {
  if (!dateStr) return '—';
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateStr.trim());
  if (match) {
    return `${match[3]}/${match[2]}/${match[1]}`;
  }
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return dateStr;
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

export function formatCalculationErrorMessage(code: string | null | undefined): string | null {
  if (!code) return null;
  return formatScoreReason(code);
}

export function getCouncilMinuteErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError<{ error?: { code?: string; message?: string }; message?: string }>(error)) {
    const code = error.response?.data?.error?.code;
    if (code && COUNCIL_MINUTE_ERROR_MESSAGES[code]) {
      return COUNCIL_MINUTE_ERROR_MESSAGES[code];
    }
    if (code) {
      const mappedReason = formatScoreReason(code);
      if (mappedReason !== code) return mappedReason;
    }
    const serverMessage = error.response?.data?.error?.message || error.response?.data?.message;
    if (serverMessage) return serverMessage;
    if (error.response?.status === 403) {
      return 'Bạn không có quyền thực hiện thao tác này trên biên bản hội đồng.';
    }
    if (error.response?.status === 404) {
      return 'Không tìm thấy dữ liệu biên bản hội đồng được yêu cầu.';
    }
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return fallback;
}
