import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';

import { PageHeader } from '@/components/ui/page-header';
import { SimilarityReportPanel } from '@/features/thesis/components/SimilarityReportPanel';
import { useReviewerAssignment } from '../hooks/useReviewerAssignment';
import {
  ReviewerAssignmentFilterBar,
  ReviewerAssignmentTable,
  ReviewerAssignmentFormCard,
  ReviewerAssignmentDetailCard,
} from '../components/reviewer-assignment';

export function ReviewerAssignmentPage() {
  const {
    keyword,
    setKeyword,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    filteredItems,
    pendingCount,
    selectedGroupId,
    setSelectedGroupId,
    selectedItem,
    groupDetailQuery,
    candidatesQuery,
    candidates,
    selectedReviewerId,
    setSelectedReviewerId,
    notice,
    setNotice,
    errorMessage,
    canManage,
    assignMutation,
    selectedBatch,
  } = useReviewerAssignment();

  const selectedIndex = filteredItems.findIndex((i) => i.groupId === selectedGroupId);

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Phân công phản biện"
        description="Chỉ hồ sơ đã được GVHD đồng ý chuyển."
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {pendingCount} hồ sơ chờ phân công
        </span>
      </PageHeader>

      {errorMessage && <InlineAlert title="Không thể hoàn tất">{errorMessage}</InlineAlert>}

      <OperationToast message={notice} onConsumed={() => setNotice(null)} />

      <ReviewerAssignmentFilterBar
        keyword={keyword}
        onKeywordChange={setKeyword}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        onApply={handleApplyFilter}
        onReset={handleResetFilter}
      />

      <ReviewerAssignmentTable
        items={filteredItems}
        selectedGroupId={selectedGroupId}
        onSelectGroupId={setSelectedGroupId}
      />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8">
          <ReviewerAssignmentFormCard
            selectedItem={selectedItem}
            candidates={candidates}
            selectedReviewerId={selectedReviewerId}
            onSelectReviewerId={setSelectedReviewerId}
            onSubmitAssignment={() => assignMutation.mutate()}
            isSubmitting={assignMutation.isPending}
            canManage={canManage}
            isLoadingCandidates={candidatesQuery.isLoading}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <ReviewerAssignmentDetailCard
            selectedItem={selectedItem}
            groupDetail={groupDetailQuery.data}
            batchName={selectedBatch?.batchName}
            groupIndex={selectedIndex >= 0 ? selectedIndex : 0}
            isLoading={groupDetailQuery.isLoading}
          />
        </div>
      </div>
      {selectedItem && (
        <SimilarityReportPanel reportId={selectedItem.finalReportId} versionNo={selectedItem.reportVersionNo} />
      )}
    </div>
  );
}
