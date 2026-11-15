import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { useFinalReportSubmission } from '../hooks/useFinalReportSubmission';
import { FINAL_REPORT_STATUS_LABELS, type FinalReportStatus } from '../types/finalReport.types';
import { ThesisSubmissionWorkspace } from '../components/shared';
import { SimilarityReportPanel } from '../components/SimilarityReportPanel';
import {
  FinalReportSummaryCard,
  FinalReportUploadCard,
  FinalReportVersionCard,
  ReviewerFeedbackDialog,
} from '../components/finalReport';

const headerToneMap: Record<string, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'info',
  UnderEvaluation: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

export function FinalReportSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    midtermHistoryQuery,
    historyQuery,
    feedbackQuery,
    selectedFile,
    productLinks,
    setProductLinks,
    selectedImages,
    setSelectedImages,
    uploadProgress,
    isFeedbackOpen,
    setIsFeedbackOpen,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    currentStatus,
    versions,
    latestVersion,
    canSubmit,
    notEligibleReason,
    submitMutation,
    handleDownload,
    handleFileSelect,
    handleRemoveFile,
  } = useFinalReportSubmission();

  const groupDetail = groupDetailQuery.data;
  const midtermApproved = midtermHistoryQuery.data?.currentStatus === 'Approved';
  const nextVersionNo = (historyQuery.data?.currentVersionNo || 0) + 1;
  const headerStatusLabel =
    FINAL_REPORT_STATUS_LABELS[currentStatus as FinalReportStatus] || 'Chưa nộp';
  const headerStatusTone = headerToneMap[currentStatus] || 'neutral';

  const isLockedOrDone = !canSubmit || currentStatus === 'Approved';
  const showUploadCard =
    (canSubmit && currentStatus !== 'Approved') ||
    !midtermApproved ||
    (!historyQuery.isLoading && versions.length === 0 && currentStatus !== 'Approved');

  const uploadCard = showUploadCard ? (
    <FinalReportUploadCard
      selectedFile={selectedFile}
      productLinks={productLinks}
      selectedImages={selectedImages}
      uploadProgress={uploadProgress}
      onProductLinksChange={setProductLinks}
      onImagesChange={setSelectedImages}
      onFileSelect={(file) => {
        if (file) {
          handleFileSelect(file);
        } else {
          handleRemoveFile();
        }
      }}
      onSubmit={() => {
        if (selectedFile && canSubmit) {
          submitMutation.mutate(selectedFile);
        }
      }}
      onOpenFeedback={() => setIsFeedbackOpen(true)}
      isSubmitting={submitMutation.isPending}
      canSubmit={canSubmit}
      midtermApproved={midtermApproved}
      currentStatus={currentStatus}
      nextVersionNo={nextVersionNo}
      disabled={!canSubmit || submitMutation.isPending}
      disabledReason={notEligibleReason}
      latestFileName={latestVersion?.file.fileName ?? null}
    />
  ) : null;

  const versionCard = (
    <FinalReportVersionCard
      versions={versions}
      currentStatus={currentStatus}
      currentVersionNo={historyQuery.data?.currentVersionNo}
      onDownload={(fileId) => void handleDownload(fileId)}
      onOpenFeedback={() => setIsFeedbackOpen(true)}
      isLoading={historyQuery.isLoading}
    />
  );

  const similarityPanel = historyQuery.data?.finalReportId ? (
    <SimilarityReportPanel
      reportId={historyQuery.data.finalReportId}
      versionNo={latestVersion?.versionNo}
    />
  ) : null;

  return (
    <ThesisSubmissionWorkspace
      activeTab="final-report"
      statusQuery={statusQuery}
      groupDetail={groupDetail}
      groupDetailLoading={groupDetailQuery.isLoading}
      headerBadge={<StatusBadge tone={headerStatusTone}>{headerStatusLabel}</StatusBadge>}
      actionError={actionError}
      onDismissError={() => setActionError(null)}
      actionSuccess={actionSuccess}
      onDismissSuccess={() => setActionSuccess(null)}
      sidebar={
        <FinalReportSummaryCard
          groupDetail={groupDetail}
          isLoading={groupDetailQuery.isLoading}
          isError={groupDetailQuery.isError}
          onRetry={() => void groupDetailQuery.refetch()}
        />
      }
    >
      {historyQuery.isError && (
        <InlineAlert
          tone="error"
          title="Không thể tải thông tin báo cáo cuối kỳ"
          action={
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => void historyQuery.refetch()}
            >
              <RefreshCw className="size-3.5 mr-1.5" />
              Thử lại
            </Button>
          }
        >
          Đã xảy ra lỗi khi đồng bộ dữ liệu báo cáo cuối kỳ. Vui lòng thử lại sau.
        </InlineAlert>
      )}

      {currentStatus === 'UnderEvaluation' && (
        <InlineAlert tone="warning" title="Báo cáo cuối kỳ đang được GVHD chấm điểm">
          Hồ sơ tạm khóa chỉnh sửa trong khi giảng viên hướng dẫn đánh giá trước phản biện.
        </InlineAlert>
      )}

      {currentStatus === 'Approved' && (
        <InlineAlert tone="success" title="GVHD đã đồng ý chuyển phản biện">
          Báo cáo toàn văn của nhóm đã đủ điều kiện chuyển Hội đồng / Giảng viên phản biện. Nhấn Xem góp ý GVPB khi có nhận xét chính thức.
        </InlineAlert>
      )}

      {currentStatus === 'Rejected' && (
        <InlineAlert tone="error" title="Cần chỉnh sửa lại báo cáo cuối kỳ">
          Báo cáo cần được chỉnh sửa theo trao đổi với GVHD. Hãy tải lên phiên bản mới để nộp lại.
        </InlineAlert>
      )}

      {isLockedOrDone ? (
        <>
          {versionCard}
          {similarityPanel}
          {uploadCard}
        </>
      ) : (
        <>
          {uploadCard}
          {versionCard}
          {similarityPanel}
        </>
      )}

      <ReviewerFeedbackDialog
        open={isFeedbackOpen}
        onClose={() => setIsFeedbackOpen(false)}
        comment={feedbackQuery.data?.comment}
        isLoading={feedbackQuery.isLoading}
      />
    </ThesisSubmissionWorkspace>
  );
}
