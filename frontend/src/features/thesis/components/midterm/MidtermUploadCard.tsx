import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { MidtermReportStatus } from '../../types/midterm.types';
import { SubmissionDropzoneCard } from '../shared';
import { MidtermStatusBadge } from './MidtermStatusBadge';

interface MidtermUploadCardProps {
  selectedFile: File | null;
  uploadProgress?: number | null;
  onFileSelect: (file: File | null) => void;
  onSubmit: () => void;
  isSubmitting?: boolean;
  canSubmit?: boolean;
  isEligibleForMidterm?: boolean;
  currentStatus: MidtermReportStatus;
  nextVersionNo: number;
  existingFileName?: string | null;
  disabled?: boolean;
  disabledReason?: string | null;
}

export function MidtermUploadCard({
  selectedFile,
  uploadProgress = null,
  onFileSelect,
  onSubmit,
  isSubmitting = false,
  canSubmit = true,
  isEligibleForMidterm = true,
  currentStatus,
  nextVersionNo,
  existingFileName,
  disabled = false,
  disabledReason,
}: MidtermUploadCardProps) {
  const lockedTitle = (() => {
    if (!isEligibleForMidterm) return 'Chưa mở khóa Cổng 2 · Báo cáo giữa kỳ';
    if (currentStatus === 'Approved') return 'Báo cáo giữa kỳ đã đạt Cổng 2';
    if (currentStatus === 'Submitted') return 'Báo cáo giữa kỳ đang chờ thẩm định';
    if (currentStatus === 'Rejected') return 'Báo cáo giữa kỳ không đạt';
    return 'Khu vực nộp báo cáo giữa kỳ tạm khóa';
  })();

  const lockedDescription =
    disabledReason ||
    (currentStatus === 'Approved'
      ? 'Văn phòng Khoa đã phê duyệt Đạt cho Cổng 2. Nhóm đủ điều kiện chuẩn bị nộp Báo cáo cuối kỳ.'
      : currentStatus === 'Submitted'
        ? 'Hồ sơ tạm khóa trong thời gian chờ kết quả thẩm định. Nếu có yêu cầu chỉnh sửa, hệ thống sẽ mở lại để nộp phiên bản mới.'
        : null);

  return (
    <SubmissionDropzoneCard
      title={`Nộp báo cáo giữa kỳ · Phiên bản v${nextVersionNo}`}
      subtitle="Định dạng hỗ trợ: PDF, DOC, DOCX · tối đa 20 MB"
      headerBadge={<MidtermStatusBadge status={currentStatus} />}
      selectedFile={selectedFile}
      onFileSelect={onFileSelect}
      existingFileName={existingFileName}
      disabled={disabled}
      lockedTitle={lockedTitle}
      lockedDescription={lockedDescription}
      lockedAction={
        !isEligibleForMidterm ? (
          <Button asChild variant="outline" size="sm">
            <Link to="/thesis/proposal">
              <ArrowLeft className="size-3.5 mr-1.5" />
              Về Đề cương chi tiết
            </Link>
          </Button>
        ) : undefined
      }
      compactWhenLocked
      submitLabel={
        nextVersionNo > 1 ? `Nộp lại giữa kỳ v${nextVersionNo}` : 'Nộp báo cáo giữa kỳ'
      }
      submitLoadingLabel="Đang nộp báo cáo giữa kỳ…"
      canSubmit={canSubmit}
      isSubmitting={isSubmitting}
      uploadProgress={uploadProgress}
      onSubmit={onSubmit}
    />
  );
}
