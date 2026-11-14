import { CheckCircle2, Lock, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { useMidtermSubmission } from '../hooks/useMidtermSubmission';
import { ThesisSubmissionWorkspace } from '../components/shared';
import { SyllabusTopicInfoCard } from '../components/submission';
import {
  MidtermHistoryTable,
  MidtermStatusBadge,
  MidtermUploadCard,
} from '../components/midterm';

export function MidtermSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    syllabusHistoryQuery,
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
    isEligibleForMidterm,
    notEligibleReason,
  } = useMidtermSubmission();

  const groupDetail = groupDetailQuery.data;
  const versions = historyQuery.data?.versions || [];
  const latestVersion = versions[versions.length - 1];
  const nextVersionNo = (historyQuery.data?.currentVersionNo || 0) + 1;
  const latestReview = [...versions]
    .reverse()
    .find((version) => version.review != null)?.review;

  const syllabusApproved = syllabusHistoryQuery.data?.currentStatus === 'Approved';

  const showUploadCard =
    canSubmit ||
    !isEligibleForMidterm ||
    (!historyQuery.isLoading && versions.length === 0 && currentStatus !== 'Approved');

  const uploadCard = showUploadCard ? (
    <MidtermUploadCard
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
      isEligibleForMidterm={isEligibleForMidterm}
      currentStatus={currentStatus}
      nextVersionNo={nextVersionNo}
      existingFileName={latestVersion?.file.fileName ?? null}
      disabled={!canSubmit || submitMutation.isPending}
      disabledReason={notEligibleReason}
    />
  ) : null;

  const historyCard = (
    <MidtermHistoryTable
      versions={versions}
      currentStatus={currentStatus}
      groupCode={groupDetail?.groupCode}
      onDownload={(fileId) => void handleDownload(fileId)}
      isLoading={historyQuery.isLoading}
    />
  );

  return (
    <ThesisSubmissionWorkspace
      activeTab="midterm"
      statusQuery={statusQuery}
      groupDetail={groupDetail}
      groupDetailLoading={groupDetailQuery.isLoading}
      headerBadge={<MidtermStatusBadge status={currentStatus} />}
      actionError={actionError}
      onDismissError={() => setActionError(null)}
      actionSuccess={actionSuccess}
      onDismissSuccess={() => setActionSuccess(null)}
      sidebar={
        <SyllabusTopicInfoCard
          title="Hồ sơ giữa kỳ"
          groupDetail={groupDetail}
          isLoading={groupDetailQuery.isLoading}
          isError={groupDetailQuery.isError}
          onRetry={() => void groupDetailQuery.refetch()}
          topBanner={
            !syllabusHistoryQuery.isLoading ? (
              <div
                className={
                  syllabusApproved
                    ? 'flex items-center gap-2 rounded-xl border border-emerald-200/80 bg-emerald-50/70 px-3 py-2 text-xs font-medium text-emerald-800'
                    : 'flex items-center gap-2 rounded-xl border border-amber-200/80 bg-amber-50/75 px-3 py-2 text-xs font-medium text-amber-800'
                }
              >
                {syllabusApproved ? (
                  <>
                    <CheckCircle2 className="size-4 shrink-0 text-emerald-600" />
                    <span>Điều kiện Cổng 1: Đề cương đã đạt</span>
                  </>
                ) : (
                  <>
                    <Lock className="size-4 shrink-0 text-amber-600" />
                    <span>Điều kiện Cổng 1: Chưa hoàn thành Đề cương</span>
                  </>
                )}
              </div>
            ) : undefined
          }
          metaItems={[
            {
              label: 'Thẩm định Cổng 2',
              value: 'Văn phòng Khoa đánh giá: Đạt / Yêu cầu chỉnh sửa / Không đạt.',
            },
            {
              label: 'Thời hạn dự kiến',
              value: 'Giữa học kỳ · Theo kế hoạch đợt khóa luận',
              accent: true,
            },
          ]}
          rulesTitle="Yêu cầu báo cáo giữa kỳ"
          rules={[
            'Đề cương chi tiết (Cổng 1) phải được Văn phòng Khoa phê duyệt Đạt.',
            'Trình bày đầy đủ tiến độ triển khai kiến trúc, kết quả thực nghiệm ban đầu.',
            'Định dạng PDF, DOC hoặc DOCX (tối đa 20 MB).',
          ]}
        />
      }
    >
      {historyQuery.isError && (
        <InlineAlert
          tone="error"
          title="Không thể tải lịch sử báo cáo giữa kỳ"
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
          Đã xảy ra lỗi khi đồng bộ dữ liệu Cổng 2. Vui lòng thử lại sau.
        </InlineAlert>
      )}

      {currentStatus === 'Approved' && latestReview?.feedback && (
        <InlineAlert tone="success" title="Đã hoàn thành Cổng 2 · Báo cáo giữa kỳ">
          <RichTextContent content={latestReview.feedback} className="text-sm" />
        </InlineAlert>
      )}

      {currentStatus === 'NeedRevision' && (
        <InlineAlert tone="warning" title="Văn phòng Khoa yêu cầu chỉnh sửa báo cáo giữa kỳ">
          {latestReview?.feedback ? (
            <RichTextContent content={latestReview.feedback} className="text-sm" />
          ) : (
            'Vui lòng cập nhật lại nội dung báo cáo giữa kỳ và nộp phiên bản mới để thẩm định lại.'
          )}
        </InlineAlert>
      )}

      {currentStatus === 'Rejected' && (
        <InlineAlert tone="error" title="Báo cáo giữa kỳ không đạt yêu cầu">
          {latestReview?.feedback ? (
            <RichTextContent content={latestReview.feedback} className="text-sm" />
          ) : (
            'Báo cáo giữa kỳ không được thông qua ở Cổng 2. Nhóm vui lòng liên hệ GVHD hoặc Văn phòng Khoa.'
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
