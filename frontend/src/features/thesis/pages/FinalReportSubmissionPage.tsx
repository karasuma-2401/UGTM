import { Info, MessageSquareQuote, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { useFinalReportSubmission } from '../hooks/useFinalReportSubmission';
import { FINAL_REPORT_STATUS_LABELS, type FinalReportStatus } from '../types/finalReport.types';
import { SubmissionDropzoneCard, ThesisSubmissionWorkspace, VersionHistoryTimeline } from '../components/shared';
import { SimilarityReportPanel } from '../components/SimilarityReportPanel';
import {
  FinalReportSummaryCard,
  FinalReportUploadCard,
  FinalReportVersionCard,
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
    revisedEligibilityQuery,
    revisedHistoryQuery,
    selectedRevisedFile,
    setSelectedRevisedFile,
    canSubmitRevision,
    submitRevisedMutation,
    selectedFile,
    productLinks,
    setProductLinks,
    selectedImages,
    setSelectedImages,
    uploadProgress,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    currentStatus,
    versions,
    latestVersion,
    canSubmit,
    notEligibleReason,
    stageBlockedReason,
    submitMutation,
    handleDownload,
    handleFileSelect,
    handleRemoveFile,
  } = useFinalReportSubmission();

  const groupDetail = groupDetailQuery.data;
  const midtermApproved = midtermHistoryQuery.data?.currentStatus === 'Approved';
  const nextVersionNo = (historyQuery.data?.currentVersionNo || 0) + 1;
  const headerStatusLabel =
    revisedEligibilityQuery.data?.canSubmit
      ? 'Được nộp bản sửa'
      : feedbackQuery.data?.comment
        ? 'Đã có góp ý phản biện'
        : FINAL_REPORT_STATUS_LABELS[currentStatus as FinalReportStatus] || 'Chưa nộp';
  const headerStatusTone = revisedEligibilityQuery.data?.canSubmit || feedbackQuery.data?.comment
    ? 'info'
    : headerToneMap[currentStatus] || 'neutral';
  const feedbackComment = feedbackQuery.data?.comment?.trim() || '';
  const revisedCurrentVersionNo = revisedHistoryQuery.data?.currentVersionNo ?? 0;

  const isLockedOrDone = !canSubmit || currentStatus === 'Approved';
  const showUploadCard = canSubmit ||
    (historyQuery.isSuccess && !historyQuery.data);

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
      hasReviewerFeedback={Boolean(feedbackComment)}
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

      {feedbackQuery.isError && (
        <InlineAlert
          tone="warning"
          title="Chưa thể tải góp ý phản biện"
          action={
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => void feedbackQuery.refetch()}
            >
              <RefreshCw className="size-3.5 mr-1.5" />
              Thử lại
            </Button>
          }
        >
          Hệ thống chưa thể đồng bộ góp ý đã phát hành. Bạn vẫn có thể xem lịch sử báo cáo.
        </InlineAlert>
      )}

      {midtermApproved &&
        stageBlockedReason &&
        (currentStatus === 'Draft' || currentStatus === 'Submitted') && (
          <InlineAlert tone="warning" title="Ngoài giai đoạn nộp Báo cáo cuối kỳ">
            {stageBlockedReason}
          </InlineAlert>
        )}

      {currentStatus === 'UnderEvaluation' && !feedbackComment && (
        <InlineAlert tone="warning" title="Báo cáo cuối kỳ đang trong quá trình đánh giá">
          Hồ sơ đã khóa chỉnh sửa từ khi bắt đầu đánh giá. Bạn có thể xem góp ý phản biện khi được phát hành.
        </InlineAlert>
      )}

      {currentStatus === 'Approved' && (
        <InlineAlert tone="success" title="GVHD đã đồng ý chuyển phản biện">
          Báo cáo toàn văn đã được chuyển sang giai đoạn phản biện. Góp ý GVPB sẽ hiển thị tại đây khi được phát hành.
        </InlineAlert>
      )}

      {currentStatus === 'Rejected' && (
        <InlineAlert tone="error" title="Báo cáo chưa được thông qua">
          Phiên bản này đã khóa. Vui lòng liên hệ GVHD để biết hướng xử lý tiếp theo; cổng nộp báo cáo cuối kỳ không nhận phiên bản mới sau khi bắt đầu đánh giá.
        </InlineAlert>
      )}

      {feedbackComment && (
        <Card className="overflow-hidden border-line/80 bg-surface-card shadow-xs">
          <div className="flex items-start gap-3 border-b border-line-subtle px-5 py-4 sm:px-6">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-tone-info-bg text-tone-info-text">
              <MessageSquareQuote className="size-5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <h2 className="text-base font-semibold text-ink-primary">Góp ý từ Giảng viên phản biện</h2>
              <p className="mt-0.5 text-sm text-ink-muted">Nhận xét chính thức cho bản báo cáo đã nộp.</p>
            </div>
          </div>
          <div className="px-5 py-5 sm:px-6">
            <RichTextContent content={feedbackComment} className="break-words text-sm leading-7 text-ink-heading" />
          </div>
          {!revisedEligibilityQuery.data?.canSubmit && (
            <div className="flex flex-wrap items-start justify-between gap-3 border-t border-line-subtle bg-surface-subtle/70 px-5 py-4 sm:px-6">
              <div className="flex min-w-0 items-start gap-2.5 text-sm leading-6 text-ink-secondary">
                <Info className="mt-1 size-4 shrink-0 text-tone-info-icon" aria-hidden="true" />
                <p>
                  {revisedEligibilityQuery.isLoading
                    ? 'Đang kiểm tra quyền nộp bản chỉnh sửa…'
                    : revisedEligibilityQuery.isError
                      ? 'Chưa thể kiểm tra quyền nộp bản chỉnh sửa.'
                      : revisedEligibilityQuery.data?.reasonCode === 'OutsideAllowedWindow'
                        ? 'Đã ngoài thời gian nhận bản chỉnh sửa. Vui lòng liên hệ Khoa để được hướng dẫn.'
                        : 'Chưa mở quyền nộp bản chỉnh sửa để ra Hội đồng. Vui lòng theo dõi thông báo từ Khoa.'}
                </p>
              </div>
              {revisedEligibilityQuery.isError && (
                <Button type="button" variant="outline" size="sm" onClick={() => void revisedEligibilityQuery.refetch()}>
                  <RefreshCw className="mr-1.5 size-3.5" aria-hidden="true" />Thử lại
                </Button>
              )}
            </div>
          )}
        </Card>
      )}

      {revisedHistoryQuery.isError && (
        <InlineAlert
          tone="warning"
          title="Chưa thể tải lịch sử bản chỉnh sửa"
          action={<Button type="button" variant="outline" size="sm" onClick={() => void revisedHistoryQuery.refetch()}>Thử lại</Button>}
        >
          Hãy tải lại lịch sử trước khi nộp để tránh ghi đè nhầm phiên bản.
        </InlineAlert>
      )}

      {canSubmitRevision && (
        <SubmissionDropzoneCard
          title={`Bản chỉnh sửa sau phản biện · Phiên bản v${revisedCurrentVersionNo + 1}`}
          subtitle="Nộp bản báo cáo đã chỉnh sửa theo góp ý GVPB để phục vụ Hội đồng. PDF, DOC hoặc DOCX · tối đa 20 MiB."
          headerBadge={<StatusBadge tone="success">Được nộp bản chỉnh sửa</StatusBadge>}
          selectedFile={selectedRevisedFile}
          onFileSelect={setSelectedRevisedFile}
          existingFileName={revisedHistoryQuery.data?.versions.at(-1)?.file.fileName}
          canSubmit={canSubmitRevision}
          isSubmitting={submitRevisedMutation.isPending}
          onSubmit={() => { if (selectedRevisedFile) submitRevisedMutation.mutate(selectedRevisedFile); }}
          submitLabel="Nộp bản chỉnh sửa"
          submitLoadingLabel="Đang nộp bản chỉnh sửa…"
        />
      )}

      {revisedHistoryQuery.data && revisedHistoryQuery.data.versions.length > 0 && (
        <VersionHistoryTimeline
          title="Bản chỉnh sửa sau phản biện"
          items={revisedHistoryQuery.data.versions.map((version) => ({
            key: version.versionNo,
            versionNo: version.versionNo,
            fileId: version.file.id,
            fileName: version.file.fileName,
            fileSize: version.file.fileSize,
            timestampUtc: version.file.uploadedAtUtc,
            statusBadge: <StatusBadge tone={version.versionNo === revisedCurrentVersionNo ? 'info' : 'neutral'}>
              {version.versionNo === revisedCurrentVersionNo ? 'Bản mới nhất' : 'Bản cũ'}
            </StatusBadge>,
          }))}
          onDownload={(fileId) => void handleDownload(fileId)}
        />
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

    </ThesisSubmissionWorkspace>
  );
}
