import { Flag, RefreshCw } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { useStudentMilestones } from '../hooks/useStudentMilestones';
import { useThesisJourneySummary } from '../hooks/useThesisJourneySummary';
import { ThesisSubmissionWorkspace } from '../components/shared';
import {
  MilestoneSelectorStrip,
  MilestoneStatusBadge,
  StudentMilestoneBriefCard,
  StudentMilestoneHistoryTable,
  StudentMilestoneUploadCard,
} from '../components/milestone';

export function StudentMilestoneSubmissionPage() {
  const {
    statusQuery,
    groupDetailQuery,
    milestonesQuery,
    milestones,
    selectedMilestoneId,
    setSelectedMilestoneId,
    selectedMilestone,
    selectedMilestoneIndex,
    reportQuery,
    report,
    versions,
    currentStatus,
    nextVersionNo,
    canSubmit,
    selectedFile,
    setSelectedFile,
    productLinks,
    setProductLinks,
    selectedImages,
    setSelectedImages,
    uploadProgress,
    studentNote,
    setStudentNote,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
  } = useStudentMilestones();

  const groupId = statusQuery.data?.groupId;
  const groupDetail = groupDetailQuery.data;
  const journey = useThesisJourneySummary(groupId);

  const lockedTitle =
    currentStatus === 'Accepted'
      ? 'Mốc báo cáo này đã Đạt'
      : currentStatus === 'Submitted'
        ? 'Bài nộp đang chờ GVHD đánh giá'
        : 'Khu vực nộp sản phẩm tạm khóa';

  const lockedReason =
    currentStatus === 'Accepted'
      ? 'GVHD đã xác nhận Đạt cho mốc báo cáo này. Nhóm không cần nộp thêm phiên bản mới.'
      : currentStatus === 'Submitted'
        ? 'Hệ thống tạm khóa chỉnh sửa trong lúc GVHD xem xét phiên bản hiện tại.'
        : null;

  const handleSelectMilestone = (id: string) => {
    setSelectedMilestoneId(id);
    setSelectedFile(null);
    setActionError(null);
    setActionSuccess(null);
  };

  const nextPendingIndex =
    currentStatus === 'Accepted'
      ? (() => {
          const revisionIndex = journey.milestoneItems.findIndex(
            (item) => item.milestone.id !== selectedMilestoneId && item.status === 'NeedRevision',
          );
          if (revisionIndex >= 0) return revisionIndex;
          return journey.milestoneItems.findIndex(
            (item) => item.milestone.id !== selectedMilestoneId && item.status === 'NotSubmitted',
          );
        })()
      : -1;
  const nextPendingItem = nextPendingIndex >= 0 ? journey.milestoneItems[nextPendingIndex] : null;
  const nextActionLabel = nextPendingItem
    ? `Tiếp theo: Mốc ${String(nextPendingIndex + 1).padStart(2, '0')}`
    : undefined;

  const showUploadCard = canSubmit || (!reportQuery.isLoading && versions.length === 0);

  const uploadCard = showUploadCard ? (
    <StudentMilestoneUploadCard
      versionNo={nextVersionNo}
      selectedFile={selectedFile}
      productLinks={productLinks}
      selectedImages={selectedImages}
      studentNote={studentNote}
      uploadProgress={uploadProgress}
      onFileSelect={(file) => {
        setActionError(null);
        setActionSuccess(null);
        setSelectedFile(file);
      }}
      onProductLinksChange={setProductLinks}
      onImagesChange={setSelectedImages}
      onNoteChange={setStudentNote}
      onSubmit={() => submitMutation.mutate()}
      isSubmitting={submitMutation.isPending}
      canSubmit={canSubmit}
      disabled={!canSubmit || submitMutation.isPending}
      lockedTitle={lockedTitle}
      disabledReason={lockedReason}
    />
  ) : null;

  const historyCard = (
    <StudentMilestoneHistoryTable
      versions={versions}
      deadlineUtc={selectedMilestone?.deadlineUtc}
      onDownload={(fileId) => void handleDownload(fileId)}
      isLoading={reportQuery.isLoading}
      omitLatestReview
    />
  );

  return (
    <ThesisSubmissionWorkspace
      activeTab="milestones"
      statusQuery={statusQuery}
      groupDetail={groupDetail}
      groupDetailLoading={groupDetailQuery.isLoading}
      headerBadge={
        selectedMilestone ? (
          <MilestoneStatusBadge
            status={currentStatus}
            versionNo={report?.currentVersionNo ? report.currentVersionNo : null}
          />
        ) : undefined
      }
      actionError={actionError}
      onDismissError={() => setActionError(null)}
      actionSuccess={actionSuccess}
      onDismissSuccess={() => setActionSuccess(null)}
    >
      {milestonesQuery.isError ? (
        <InlineAlert
          tone="error"
          title="Không thể tải danh sách mốc báo cáo"
          action={
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => void milestonesQuery.refetch()}
            >
              <RefreshCw className="size-3.5 mr-1.5" />
              Thử lại
            </Button>
          }
        >
          Đã xảy ra lỗi khi tải danh sách mốc tiến độ của nhóm. Vui lòng thử lại sau.
        </InlineAlert>
      ) : milestonesQuery.isLoading ? (
        <div className="space-y-5">
          <Skeleton className="h-36 w-full rounded-2xl" />
          <Skeleton className="h-56 w-full rounded-2xl" />
        </div>
      ) : milestones.length === 0 ? (
        <Card className="p-10 bg-white border-slate-200/80 shadow-xs rounded-2xl text-center">
          <div className="max-w-md mx-auto space-y-2.5">
            <div className="size-12 rounded-2xl bg-slate-100 text-slate-400 flex items-center justify-center mx-auto">
              <Flag className="size-6" />
            </div>
            <h2 className="text-base font-bold text-slate-900">Chưa có mốc báo cáo tiến độ</h2>
            <p className="text-sm text-slate-500 leading-relaxed">
              Giảng viên hướng dẫn chưa thiết lập mốc báo cáo tiến độ nào cho nhóm. Bạn vẫn có thể theo dõi trạng thái Đề cương chi tiết ở Cổng 1.
            </p>
          </div>
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
          <aside className="lg:col-span-4 lg:sticky lg:top-6 space-y-4 min-w-0">
            <MilestoneSelectorStrip
              items={journey.milestoneItems}
              selectedMilestoneId={selectedMilestoneId}
              onSelect={handleSelectMilestone}
              isLoading={milestonesQuery.isLoading}
            />

            {groupDetail && (
              <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs px-4 py-3.5 space-y-2 text-xs">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-semibold text-slate-800 truncate" title={groupDetail.topicTitleVi}>
                    {groupDetail.topicCode || 'Đề tài nhóm'}
                  </span>
                  {groupDetail.groupCode && (
                    <span className="font-mono font-semibold text-slate-700 bg-slate-100 px-2 py-0.5 rounded-md shrink-0">
                      {groupDetail.groupCode}
                    </span>
                  )}
                </div>
                <p className="text-slate-600 line-clamp-2 leading-relaxed">
                  {groupDetail.topicTitleVi}
                </p>
                {groupDetail.advisor?.fullName && (
                  <p className="text-slate-500 pt-1 border-t border-slate-100 truncate">
                    GVHD: <span className="font-medium text-slate-700">{groupDetail.advisor.fullName}</span>
                  </p>
                )}
              </div>
            )}
          </aside>

          <div
            key={selectedMilestoneId ?? 'milestone-detail'}
            className="lg:col-span-8 space-y-5 min-w-0 fluent-detail-enter"
          >
            {selectedMilestone && (
              <StudentMilestoneBriefCard
                milestone={selectedMilestone}
                milestoneIndex={selectedMilestoneIndex >= 0 ? selectedMilestoneIndex : 0}
                currentStatus={currentStatus}
                currentVersionNo={report?.currentVersionNo}
                versions={versions}
                nextActionLabel={nextActionLabel}
                onJumpToNextMilestone={
                  nextPendingItem
                    ? () => handleSelectMilestone(nextPendingItem.milestone.id)
                    : undefined
                }
              />
            )}

            {reportQuery.isError && (
              <InlineAlert
                tone="warning"
                title="Không tải được chi tiết bài nộp"
                action={
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void reportQuery.refetch()}
                  >
                    <RefreshCw className="size-3.5 mr-1.5" />
                    Tải lại
                  </Button>
                }
              >
                Chưa thể đồng bộ lịch sử các phiên bản đã nộp cho mốc này.
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
          </div>
        </div>
      )}
    </ThesisSubmissionWorkspace>
  );
}
