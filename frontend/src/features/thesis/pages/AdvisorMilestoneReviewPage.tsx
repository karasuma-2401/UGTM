import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { useNavigate } from 'react-router-dom';
import { useMilestoneReview } from '../hooks/useMilestoneReview';
import {
  AdvisorMilestoneSubmissionViewCard,
  AdvisorMilestoneFeedbackForm,
  AdvisorMilestoneSidebarCard,
  MilestoneStatusBadge,
} from '../components/milestone';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { ArrowLeft, AlertCircle, CalendarCheck, ShieldAlert, RotateCcw } from 'lucide-react';
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
      <div className="max-w-7xl mx-auto py-10 px-4 text-center">
        <div className="inline-flex items-center gap-2 text-slate-500 text-sm">
          <span className="size-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
          Đang tải dữ liệu báo cáo tiến độ...
        </div>
      </div>
    );
  }

  if (reportQuery.isError) {
    const errorKind = classifyThesisError(reportQuery.error);
    const message = getThesisErrorMessage(
      reportQuery.error,
      'Không thể tải báo cáo tiến độ của mốc công việc này.',
    );

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <div className="p-8 text-center bg-white rounded-xl border border-slate-200 shadow-xs space-y-4">
          <div className="size-12 mx-auto rounded-full bg-rose-50 text-rose-600 flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-amber-600" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-slate-800">
            {errorKind === 'forbidden'
              ? 'Không có quyền truy cập báo cáo tiến độ'
              : 'Không tải được dữ liệu báo cáo tiến độ'}
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">{message}</p>
          <div className="flex items-center justify-center gap-3">
            {errorKind !== 'forbidden' && (
              <Button
                type="button"
                onClick={() => void reportQuery.refetch()}
                className="bg-primary text-white"
              >
                <RotateCcw className="size-4 mr-2" />
                Thử lại
              </Button>
            )}
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate('/advisor/milestones')}
              className="border-slate-300"
            >
              <ArrowLeft className="size-4 mr-2" />
              Quay lại danh sách mốc
            </Button>
          </div>
        </div>
      </div>
    );
  }

  if (!report && !reportQuery.isLoading) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <div className="p-8 text-center bg-white rounded-xl border border-slate-200 shadow-xs space-y-4">
          <div className="size-12 mx-auto rounded-full bg-amber-50 text-amber-600 flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-slate-800">
            Chưa có báo cáo nộp cho mốc này
          </h2>
          <p className="text-sm text-slate-500 max-w-md mx-auto">
            Nhóm sinh viên chưa nộp sản phẩm hoặc báo cáo tiến độ cho mốc công việc này. Vui lòng quay lại sau khi sinh viên đã nộp bài.
          </p>
          <Button
            type="button"
            variant="outline"
            onClick={() => navigate('/advisor/milestones')}
            className="border-slate-300"
          >
            <ArrowLeft className="size-4 mr-2" />
            Quay lại danh sách mốc
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <div className="flex items-center justify-between">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate('/advisor/milestones')}
          className="text-xs text-slate-600 hover:text-slate-900 -ml-2"
        >
          <ArrowLeft className="size-3.5 mr-1.5" />
          Quay lại danh sách mốc
        </Button>
        <span className="text-xs text-slate-400 font-medium">
          Khóa luận / Giảng viên
        </span>
      </div>

      <PageHeader
        title="Đánh giá milestone"
        description="Chỉ sản phẩm của nhóm bạn hướng dẫn."
        icon={<CalendarCheck />}
      >
        <MilestoneStatusBadge
          status={currentStatus}
          versionNo={versionNo}
          className="text-xs px-3 py-1.5"
        />
      </PageHeader>

      {actionError && <InlineAlert title="Không thể hoàn tất">{actionError}</InlineAlert>}

      <OperationToast message={actionSuccess} onConsumed={() => setActionSuccess(null)} />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <AdvisorMilestoneSubmissionViewCard
            versionNo={versionNo}
            groupCode={groupCode}
            file={latestVersion?.file}
            gitUrl={latestVersion?.gitUrl}
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

        <div className="lg:col-span-4 sticky top-6">
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

