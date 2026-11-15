import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { FINAL_REPORT_STATUS_LABELS, type FinalReportStatus } from '../../types/finalReport.types';
import {
  SubmissionBlockComposer,
  SubmissionDropzoneCard,
  type ProductLinkDraft,
} from '../shared';

interface FinalReportUploadCardProps {
  selectedFile: File | null;
  productLinks: ProductLinkDraft[];
  selectedImages: File[];
  uploadProgress?: number | null;
  onProductLinksChange: (links: ProductLinkDraft[]) => void;
  onImagesChange: (files: File[]) => void;
  onFileSelect: (file: File | null) => void;
  onSubmit: () => void;
  onOpenFeedback?: () => void;
  isSubmitting?: boolean;
  canSubmit?: boolean;
  midtermApproved?: boolean;
  currentStatus: string;
  nextVersionNo: number;
  disabled?: boolean;
  disabledReason?: string | null;
  latestFileName?: string | null;
}

const statusToneMap: Record<string, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'info',
  UnderEvaluation: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

export function FinalReportUploadCard({
  selectedFile,
  productLinks,
  selectedImages,
  uploadProgress = null,
  onProductLinksChange,
  onImagesChange,
  onFileSelect,
  onSubmit,
  onOpenFeedback: _onOpenFeedback,
  isSubmitting = false,
  canSubmit = true,
  midtermApproved = true,
  currentStatus,
  nextVersionNo,
  disabled = false,
  disabledReason,
  latestFileName,
}: FinalReportUploadCardProps) {
  const statusLabel =
    FINAL_REPORT_STATUS_LABELS[currentStatus as FinalReportStatus] || 'Chưa nộp';
  const statusTone = statusToneMap[currentStatus] || 'neutral';

  const lockedTitle = !midtermApproved
    ? 'Chưa mở khóa nộp Báo cáo cuối kỳ'
    : currentStatus === 'UnderEvaluation'
      ? 'Báo cáo cuối kỳ đang được GVHD chấm điểm'
      : 'Khu vực nộp báo cáo cuối kỳ tạm khóa';

  return (
    <SubmissionDropzoneCard
      title={`Nộp báo cáo toàn văn · Phiên bản v${nextVersionNo}`}
      subtitle="1 tệp toàn văn chính (PDF, DOC, DOCX · ≤ 20 MB), tối đa 5 liên kết và 5 ảnh minh chứng"
      headerBadge={<StatusBadge tone={statusTone}>{statusLabel}</StatusBadge>}
      selectedFile={selectedFile}
      onFileSelect={onFileSelect}
      existingFileName={latestFileName}
      disabled={disabled}
      lockedTitle={lockedTitle}
      lockedDescription={disabledReason}
      lockedAction={
        !midtermApproved ? (
          <Button asChild variant="outline" size="sm">
            <Link to="/thesis/midterm">
              <ArrowLeft className="size-3.5 mr-1.5" />
              Về Báo cáo giữa kỳ
            </Link>
          </Button>
        ) : undefined
      }
      compactWhenLocked
      submitLabel={nextVersionNo > 1 ? `Nộp lại cuối kỳ v${nextVersionNo}` : 'Nộp cuối kỳ'}
      submitLoadingLabel="Đang nộp cuối kỳ…"
      canSubmit={canSubmit}
      isSubmitting={isSubmitting}
      uploadProgress={uploadProgress}
      onSubmit={onSubmit}
      extraFields={
        <SubmissionBlockComposer
          productLinks={productLinks}
          onProductLinksChange={onProductLinksChange}
          images={selectedImages}
          onImagesChange={onImagesChange}
          linksRequired={false}
          disabled={disabled}
          title="Liên kết sản phẩm & ảnh minh chứng (tuỳ chọn)"
          subtitle="Tối đa 5 liên kết và 5 ảnh minh chứng · Hỗ trợ kéo thả hoặc dán ảnh trực tiếp bằng Ctrl+V."
        />
      }
    />
  );
}
