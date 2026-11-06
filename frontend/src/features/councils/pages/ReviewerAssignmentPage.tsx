import { AlertCircle, CheckCircle2 } from 'lucide-react';
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

      {errorMessage && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex items-start gap-3">
          <AlertCircle className="size-5 shrink-0 text-rose-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thao tác không thành công</p>
            <p className="text-xs mt-0.5 text-rose-700">{errorMessage}</p>
          </div>
        </div>
      )}

      {notice && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-start gap-3">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thông báo</p>
            <p className="text-xs mt-0.5 text-emerald-700">{notice}</p>
          </div>
        </div>
      )}

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
