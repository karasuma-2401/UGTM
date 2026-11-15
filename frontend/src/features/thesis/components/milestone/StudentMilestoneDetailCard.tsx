import type { ProgressMilestoneDto, ProgressReportStatus } from '../../types/milestone.types';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { ThesisGroupDossierCard } from '../shared';
import { formatMilestoneDateTime, getDeadlineInfo } from '../../utils/milestoneFormatters';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';

interface StudentMilestoneDetailCardProps {
  groupDetail?: StudentGroupDetailDto | null;
  milestone?: ProgressMilestoneDto | null;
  currentStatus: ProgressReportStatus;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}

export function StudentMilestoneDetailCard({
  groupDetail,
  milestone,
  currentStatus,
  isLoading = false,
  isError = false,
  onRetry,
}: StudentMilestoneDetailCardProps) {
  const deadline = milestone
    ? getDeadlineInfo(milestone.deadlineUtc, currentStatus)
    : null;

  return (
    <ThesisGroupDossierCard
      title="Hồ sơ tiến độ"
      groupDetail={groupDetail}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      metaItems={
        milestone
          ? [
              {
                label: 'Hạn chót mốc đang chọn',
                value: `${formatMilestoneDateTime(milestone.deadlineUtc)}${deadline ? ` · ${deadline.label}` : ''}`,
                accent: deadline?.urgency === 'soon',
              },
              {
                label: 'Trạng thái mốc hiện tại',
                value: <MilestoneStatusBadge status={currentStatus} />,
              },
            ]
          : []
      }
      rulesTitle="Lưu ý nộp báo cáo"
      rules={[
        'Đính kèm báo cáo đúng định dạng PDF, DOC hoặc DOCX (tối đa 20 MB).',
        'Cung cấp đường dẫn kho mã nguồn hoặc sản phẩm chính xác.',
        'Mỗi lần nộp lại tạo một phiên bản mới để lưu giữ toàn bộ lịch sử đánh giá.',
      ]}
    />
  );
}
