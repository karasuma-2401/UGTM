import { ArrowLeft, ShieldAlert } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AdvisorMilestoneFeedbackForm,
  AdvisorMilestoneSidebarCard,
  AdvisorMilestoneSubmissionViewCard,
  MilestoneStatusBadge,
} from '../components/milestone';
import { useMilestoneReview } from '../hooks/useMilestoneReview';
import { milestoneApi } from '../services/milestoneApi';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

export function AdvisorMilestoneReviewPage() {
  const navigate = useNavigate();
  const {
    reportQuery,
    report,
    groupDetail,
    selectedMilestone,
    selectedMilestoneIndex,
    latestVersion,
    currentStatus,
    feedback,
    setFeedback,
    actionError,
    actionSuccess,
    setActionSuccess,
    canReview,
    isSubmitting,
    isDownloading,
    disabledReason,
    handleAccept,
    handleNeedRevision,
    handleDownload,
  } = useMilestoneReview();

  const groupCode = groupDetail?.groupCode || 'Nhóm KLTN';
  const versionNo = latestVersion?.versionNo || report?.currentVersionNo || 1;

  if (reportQuery.isLoading) {
    return (
      <div className="space-y-6 pb-8">
        <Skeleton className="h-24 w-full rounded-2xl" />
        <div className="grid gap-6 lg:grid-cols-12">
          <Skeleton className="h-72 rounded-2xl lg:col-span-8" />
          <Skeleton className="h-72 rounded-2xl lg:col-span-4" />
        </div>
      </div>
    );
  }

  if (reportQuery.isError) {
    const errorKind = classifyThesisError(reportQuery.error);
    const message = getThesisErrorMessage(
      reportQuery.error,
      'Không tải được thông tin báo cáo tiến độ.',
    );

    return (
      <div className="space-y-4 py-6">
        <InlineAlert
          tone="error"
          title={
            errorKind === 'forbidden'
              ? 'Không có quyền đánh giá mốc này'
              : 'Không thể tải báo cáo tiến độ'
          }
          action={
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" onClick={() => navigate('/advisor/milestones')}>
                Quay lại Tiến độ sinh viên
              </Button>
              <Button type="button" onClick={() => void reportQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          }
        >
          {message}
        </InlineAlert>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="space-y-4 py-6">
        <InlineAlert
          tone="info"
          title="Chưa có bài nộp tiến độ"
          action={
            <Button type="button" variant="outline" onClick={() => navigate('/advisor/milestones')}>
              Quay lại Tiến độ sinh viên
            </Button>
          }
        >
          Nhóm sinh viên chưa nộp báo cáo nào cho mốc tiến độ này.
        </InlineAlert>
      </div>
    );
  }

  return (
    <div className="space-y-6 pb-8">
      <PageHeader
        accent
        title={selectedMilestone?.title || 'Đánh giá báo cáo tiến độ'}
        description={`Nhóm ${groupCode} · Phiên bản mới nhất v${versionNo}`}
      >
        <div className="flex flex-wrap items-center gap-2.5">
          <MilestoneStatusBadge status={currentStatus} />
          <Button
            type="button"
            variant="outline"
            onClick={() => navigate('/advisor/milestones')}
            className="gap-1.5"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
            <span>Danh sách mốc</span>
          </Button>
        </div>
      </PageHeader>

      {actionError && (
        <InlineAlert tone="error" title="Thao tác không thành công">
          <span className="inline-flex items-center gap-1.5">
            <ShieldAlert className="size-4 shrink-0" aria-hidden="true" />
            <span>{actionError}</span>
          </span>
        </InlineAlert>
      )}

      {actionSuccess && (
        <InlineAlert
          tone="success"
          title="Đã cập nhật đánh giá"
          onDismiss={() => setActionSuccess(null)}
        >
          {actionSuccess}
        </InlineAlert>
      )}

      <div className="grid items-start gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-8">
          <AdvisorMilestoneSubmissionViewCard
            versionNo={versionNo}
            groupCode={groupCode}
            file={latestVersion?.file}
            gitUrl={latestVersion?.gitUrl}
            productLinks={latestVersion?.productLinks}
            attachments={latestVersion?.attachments}
            resolveAttachmentUrl={(attachmentId) =>
              milestoneApi.getAttachmentUrl(attachmentId).then((res) => res.downloadUrl)
            }
            studentNote={latestVersion?.studentNote}
            onDownloadFile={handleDownload}
            isDownloading={isDownloading}
          />

          <AdvisorMilestoneFeedbackForm
            feedback={feedback}
            onFeedbackChange={setFeedback}
            onAccept={handleAccept}
            onNeedRevision={handleNeedRevision}
            isSubmitting={isSubmitting}
            disabled={!canReview}
            disabledReason={disabledReason}
            currentStatus={currentStatus}
          />
        </div>

        <div className="lg:col-span-4 lg:sticky lg:top-6">
          <AdvisorMilestoneSidebarCard
            milestone={selectedMilestone}
            milestoneIndex={selectedMilestoneIndex}
            groupDetail={groupDetail}
            versionNo={versionNo}
            fileName={latestVersion?.file?.fileName}
            gitUrl={latestVersion?.gitUrl}
          />
        </div>
      </div>
    </div>
  );
}
