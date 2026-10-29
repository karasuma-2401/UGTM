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
import { ArrowLeft, AlertCircle, CheckCircle2, CalendarCheck } from 'lucide-react';

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

      {actionError && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex items-start gap-3">
          <AlertCircle className="size-5 shrink-0 text-rose-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thao tác không thành công</p>
            <p className="text-xs mt-0.5 text-rose-700">{actionError}</p>
          </div>
        </div>
      )}

      {actionSuccess && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-start gap-3">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thông báo</p>
            <p className="text-xs mt-0.5 text-emerald-700">{actionSuccess}</p>
          </div>
        </div>
      )}

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

