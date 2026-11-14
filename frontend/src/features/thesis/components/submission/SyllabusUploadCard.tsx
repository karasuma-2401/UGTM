import { SubmissionDropzoneCard } from '../shared';
import type { SyllabusStatus } from '../../types/syllabus.types';
import { SyllabusStatusBadge } from './SyllabusStatusBadge';

interface SyllabusUploadCardProps {
  selectedFile: File | null;
  uploadProgress?: number | null;
  onFileSelect: (file: File | null) => void;
  onSubmit: () => void;
  isSubmitting?: boolean;
  canSubmit?: boolean;
  currentStatus: SyllabusStatus;
  nextVersionNo: number;
  existingFileName?: string | null;
  disabled?: boolean;
}

export function SyllabusUploadCard({
  selectedFile,
  uploadProgress = null,
  onFileSelect,
  onSubmit,
  isSubmitting = false,
  canSubmit = true,
  currentStatus,
  nextVersionNo,
  existingFileName,
  disabled = false,
}: SyllabusUploadCardProps) {
  const lockedCopy = (() => {
    switch (currentStatus) {
      case 'Approved':
        return {
          title: 'Đề cương đã đạt Cổng 1',
          description:
            'Văn phòng Khoa đã phê duyệt Đề cương chi tiết của nhóm. Nhóm đủ điều kiện chuyển sang giai đoạn Báo cáo giữa kỳ.',
        };
      case 'Submitted':
        return {
          title: 'Đề cương đang chờ Văn phòng Khoa thẩm định',
          description:
            'Hồ sơ tạm khóa chỉnh sửa trong thời gian chờ kết quả thẩm định. Nếu có yêu cầu chỉnh sửa, hệ thống sẽ mở lại khung nộp.',
        };
      case 'Rejected':
        return {
          title: 'Hồ sơ đề cương đã khóa',
          description:
            'Đề cương không đạt yêu cầu. Vui lòng liên hệ GVHD hoặc Văn phòng Khoa nếu cần hỗ trợ.',
        };
      default:
        return {
          title: 'Khu vực nộp đề cương tạm khóa',
          description: 'Hiện chưa thể nộp phiên bản đề cương mới.',
        };
    }
  })();

  return (
    <SubmissionDropzoneCard
      title={`Nộp đề cương chi tiết · Phiên bản v${nextVersionNo}`}
      subtitle="Định dạng hỗ trợ: PDF, DOC, DOCX · tối đa 20 MB"
      headerBadge={<SyllabusStatusBadge status={currentStatus} />}
      selectedFile={selectedFile}
      onFileSelect={onFileSelect}
      existingFileName={existingFileName}
      disabled={disabled}
      lockedTitle={lockedCopy.title}
      lockedDescription={lockedCopy.description}
      compactWhenLocked
      submitLabel={nextVersionNo > 1 ? `Nộp lại đề cương v${nextVersionNo}` : 'Nộp đề cương'}
      submitLoadingLabel="Đang nộp đề cương…"
      canSubmit={canSubmit}
      isSubmitting={isSubmitting}
      uploadProgress={uploadProgress}
      onSubmit={onSubmit}
    />
  );
}
