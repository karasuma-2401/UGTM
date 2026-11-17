import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { useSyllabusSubmission } from '../hooks/useSyllabusSubmission';
import { ThesisSubmissionWorkspace } from '../components/shared';
import {
  SyllabusHistoryTable,
  SyllabusStatusBadge,
  SyllabusTopicInfoCard,
  SyllabusUploadCard,
} from '../components/submission';

export function SyllabusSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    historyQuery,
    selectedFile,
    setSelectedFile,
    uploadProgress,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
    currentStatus,
    canSubmit,
  } = useSyllabusSubmission();

  const groupDetail = groupDetailQuery.data;
  const versions = historyQuery.data?.versions || [];
  const latestVersion = versions[versions.length - 1];
  const nextVersionNo = (historyQuery.data?.currentVersionNo || 0) + 1;
  const latestReview = [...versions]
    .reverse()
    .find((version) => version.review != null)?.review;

  const showUploadCard =
    canSubmit || (!historyQuery.isLoading && versions.length === 0 && currentStatus !== 'Approved');

  const uploadCard = showUploadCard ? (
    <SyllabusUploadCard
      selectedFile={selectedFile}
      uploadProgress={uploadProgress}
      onFileSelect={(file) => {
        setActionError(null);
        setActionSuccess(null);
        setSelectedFile(file);
      }}
      onSubmit={() => {
        if (selectedFile) {
          submitMutation.mutate(selectedFile);
        }
      }}
      isSubmitting={submitMutation.isPending}
      canSubmit={canSubmit}
      currentStatus={currentStatus}
      nextVersionNo={nextVersionNo}
      existingFileName={latestVersion?.file.fileName ?? null}
      disabled={!canSubmit || submitMutation.isPending}
    />
  ) : null;

  const historyCard = (
    <SyllabusHistoryTable
      versions={versions}
      currentStatus={currentStatus}
      groupCode={groupDetail?.groupCode}
      onDownload={(fileId) => void handleDownload(fileId)}
      isLoading={historyQuery.isLoading}
    />
  );

  return (
    <ThesisSubmissionWorkspace
      activeTab="proposal"
      statusQuery={statusQuery}
      groupDetail={groupDetail}
      groupDetailLoading={groupDetailQuery.isLoading}
      headerBadge={<SyllabusStatusBadge status={currentStatus} />}
      actionError={actionError}
      onDismissError={() => setActionError(null)}
      actionSuccess={actionSuccess}
      onDismissSuccess={() => setActionSuccess(null)}
      sidebar={
        <SyllabusTopicInfoCard
          title="Hồ sơ đề cương"
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
          title="Không thể tải lịch sử đề cương"
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
          Đã xảy ra lỗi khi đồng bộ trạng thái Cổng 1. Vui lòng thử lại sau.
        </InlineAlert>
      )}

      {currentStatus === 'Approved' && latestReview?.feedback && (
        <InlineAlert tone="success" title="Đã hoàn thành Cổng 1 · Đề cương chi tiết">
          <RichTextContent content={latestReview.feedback} className="text-sm" />
        </InlineAlert>
      )}

      {currentStatus === 'NeedRevision' && (
        <InlineAlert tone="warning" title="Văn phòng Khoa yêu cầu chỉnh sửa đề cương">
          {latestReview?.feedback ? (
            <RichTextContent content={latestReview.feedback} className="text-sm" />
          ) : (
            'Vui lòng cập nhật lại nội dung đề cương theo góp ý và nộp phiên bản mới.'
          )}
        </InlineAlert>
      )}

      {currentStatus === 'Rejected' && (
        <InlineAlert tone="error" title="Đề cương chi tiết không đạt yêu cầu">
          {latestReview?.feedback ? (
            <RichTextContent content={latestReview.feedback} className="text-sm" />
          ) : (
            'Hồ sơ đề cương không được chấp thuận ở Cổng 1. Vui lòng liên hệ GVHD hoặc Văn phòng Khoa.'
          )}
        </InlineAlert>
      )}

      {canSubmit ? (
        <>
          {uploadCard}
          {historyCard}
        </>
      ) : (
        <>
          {historyCard}
          {uploadCard}
        </>
      )}
    </ThesisSubmissionWorkspace>
  );
}
