import type {
  PublishedTopic,
  RegistrationTimeline,
  StudentRegistrationStatus,
} from './types/studentTopic.types';

export function registrationDate(value: string | null | undefined): string {
  if (!value || !Number.isFinite(Date.parse(value))) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value));
}

export function invitationExpired(status: StudentRegistrationStatus, now = Date.now()): boolean {
  return (
    status.confirmationStatus === 'Pending' &&
    Boolean(status.confirmationExpiresAtUtc) &&
    Date.parse(status.confirmationExpiresAtUtc!) <= now
  );
}

export function registrationWindow(timeline: RegistrationTimeline | undefined, now = Date.now()) {
  if (!timeline)
    return { state: 'unknown' as const, message: 'Điều kiện đăng ký sẽ được kiểm tra khi gửi.' };
  if (timeline.status !== 'Open')
    return { state: 'closed' as const, message: 'Đợt KLTN chưa mở đăng ký.' };
  const stage = timeline.stages.find(
    (item) => item.stageCode === 'REGISTRATION_AND_SYLLABUS' || item.orderIndex === 2,
  );
  if (!stage) return { state: 'closed' as const, message: 'Đợt chưa có lịch đăng ký đề tài.' };
  if (!Number.isFinite(Date.parse(stage.deadlineAtUtc)))
    return { state: 'unknown' as const, message: 'Chưa xác định được hạn đăng ký.' };
  if (timeline.currentLocalDate < stage.startDate)
    return { state: 'closed' as const, message: 'Chưa đến giai đoạn đăng ký đề tài.' };
  if (now > Date.parse(stage.deadlineAtUtc))
    return { state: 'closed' as const, message: 'Đã hết hạn đăng ký đề tài.' };
  return { state: 'open' as const, message: 'Đang mở đăng ký', deadline: stage.deadlineAtUtc };
}

export function validateStudentRegistration(
  topic: PublishedTopic,
  partnerCode: string,
  ownCode?: string | null,
  status?: StudentRegistrationStatus,
): string {
  const partner = partnerCode.trim().toUpperCase();
  if (topic.status !== 'Published' || !topic.isPublished)
    return 'Đề tài chưa được công bố hoặc đã đóng đăng ký.';
  if (topic.studentNumber !== 1 && topic.studentNumber !== 2)
    return 'Đề tài chưa có chỉ tiêu hợp lệ.';
  if (partner && topic.studentNumber < 2) return 'Đề tài chỉ nhận một sinh viên.';
  if (partner.length > 20 || /[\s,;]/.test(partner))
    return 'Vui lòng nhập một MSSV hợp lệ, tối đa 20 ký tự.';
  if (partner && partner === ownCode?.trim().toUpperCase())
    return 'Bạn không thể tự mời chính mình.';
  if (status?.isRegistrationBlocked)
    return 'Tài khoản đang bị khóa quyền đăng ký đề tài. Vui lòng liên hệ Khoa.';
  if (status?.hasRegistered && status.topicId === topic.id)
    return 'Bạn đã được ghi nhận vào đề tài này. Không cần đăng ký lại.';
  // my-status currently has no batchId. Do not guess that an older registration
  // belongs to the selected batch and block all other academic periods.
  if (status?.hasRegistered && status.batchId && status.batchId === topic.batchId)
    return 'Bạn đã có đăng ký trong đợt KLTN này.';
  if (typeof topic.availableSlots !== 'number')
    return 'Chưa có số liệu chỗ trống. Vui lòng cập nhật danh mục trước khi đăng ký.';
  if (topic.availableSlots < (partner ? 2 : 1))
    return topic.reservedSlots
      ? 'Chỗ còn lại đang được giữ cho lời mời xác nhận.'
      : 'Đề tài không còn đủ chỗ cho hình thức đăng ký này.';
  return '';
}
