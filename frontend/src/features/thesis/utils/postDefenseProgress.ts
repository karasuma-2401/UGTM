import type { FinalSubmissionEligibilityDto } from '../types/archival.types';

/** A scheduled defense date or reviewer feedback does not prove a completed defense. */
export function getPostDefenseProgress(eligibility?: FinalSubmissionEligibilityDto) {
  if (!eligibility?.isCouncilDefenseCompleted || eligibility.councilMinuteLockStatus !== 'Locked') return null;

  const status = eligibility.currentSubmissionStatus;
  const nextSteps: Record<string, { label: string; description: string }> = {
    Submitted: { label: 'Đã nộp lưu chiểu', description: 'Hồ sơ lưu chiểu đang chờ GVHD xác nhận. Theo dõi trạng thái tại trang Lưu chiểu.' },
    NeedRevision: { label: 'Cần bổ sung lưu chiểu', description: 'Hồ sơ lưu chiểu cần được chỉnh sửa. Xem yêu cầu và nộp lại tại trang Lưu chiểu.' },
    AdvisorConfirmed: { label: 'Chờ Khoa xác nhận lưu chiểu', description: 'GVHD đã xác nhận hồ sơ lưu chiểu. Hồ sơ đang chờ Khoa kiểm tra và xác nhận.' },
    ArchiveConfirmed: { label: 'Đã hoàn tất lưu chiểu', description: 'Khoa đã xác nhận hồ sơ lưu chiểu. Bạn có thể theo dõi kết quả khóa luận khi được công bố.' },
  };
  const nextStep = nextSteps[status] ?? {
    label: 'Đã hoàn tất bảo vệ',
    description: eligibility.canSubmit
      ? 'Bước tiếp theo: hoàn thiện báo cáo theo yêu cầu của Hội đồng và nộp hồ sơ lưu chiểu gồm bản PDF và DOCX.'
      : 'Bước tiếp theo là nộp hồ sơ lưu chiểu. Cổng nộp hiện chưa khả dụng; xem điều kiện tại trang Lưu chiểu hoặc liên hệ Khoa để được hướng dẫn.',
  };
  return {
    ...nextStep,
    actionLabel: status === 'ArchiveConfirmed' ? 'Theo dõi kết quả' : status === 'NotSubmitted' && eligibility.canSubmit ? 'Nộp hồ sơ lưu chiểu' : 'Xem hồ sơ lưu chiểu',
    actionTo: status === 'ArchiveConfirmed' ? '/councils/results' : '/thesis/archival',
    tone: status === 'NeedRevision' ? 'warning' as const : 'success' as const,
  };
}
