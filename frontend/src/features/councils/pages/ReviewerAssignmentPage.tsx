import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { getApiErrorMessage } from '@/shared/api';
import { OperationToast } from '@/shared/notifications';
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
    batches,
    selectedBatchId,
    setSelectedBatchId,
    eligibilityQuery,
    batchesQuery,
  } = useReviewerAssignment();
  const isSubmitting = assignMutation.isPending;
  const listError = batchesQuery.error || eligibilityQuery.error;
  const refresh = () => {
    void batchesQuery.refetch();
    void eligibilityQuery.refetch();
    if (selectedItem) {
      void groupDetailQuery.refetch();
      void candidatesQuery.refetch();
    }
  };

  return (
    <div className="space-y-6 pb-10">
      <PageHeader
        compact
        accent
        title="Phân công phản biện"
        description="Chọn hồ sơ đã được GVHD đồng ý chuyển và phân công giảng viên phản biện."
        actionsClassName="w-full sm:w-auto"
      >
        <div className="w-full sm:w-64">
          <Select
            value={selectedBatchId}
            onChange={setSelectedBatchId}
            options={batches.map((batch) => ({ value: batch.id, label: batch.batchName }))}
            aria-label="Đợt khóa luận"
            placeholder="Chọn đợt khóa luận"
            disabled={isSubmitting || batchesQuery.isLoading}
          />
        </div>
        <Button
          variant="outline"
          onClick={refresh}
          disabled={isSubmitting || eligibilityQuery.isFetching}
          leftIcon={<RefreshCw className="size-4" />}
        >
          Làm mới
        </Button>
      </PageHeader>

      <OperationToast message={notice} onConsumed={() => setNotice(null)} />
      {errorMessage && <InlineAlert title="Không thể phân công">{errorMessage}</InlineAlert>}
      {!batchesQuery.isLoading && !batchesQuery.isError && batches.length === 0 && (
        <InlineAlert tone="info" title="Chưa có đợt khóa luận">
          Cần có đợt khóa luận để xem hồ sơ phân công phản biện.
        </InlineAlert>
      )}

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
        <div className="min-w-0">
          <ReviewerAssignmentTable
            items={filteredItems}
            pendingCount={pendingCount}
            selectedGroupId={selectedGroupId}
            onSelectGroupId={setSelectedGroupId}
            isLoading={batchesQuery.isLoading || eligibilityQuery.isLoading}
            isBusy={isSubmitting || eligibilityQuery.isFetching}
            error={
              listError
                ? {
                    message: getApiErrorMessage(listError, 'Không tải được danh sách hồ sơ.'),
                    onRetry: refresh,
                  }
                : null
            }
            filters={
              <ReviewerAssignmentFilterBar
                keyword={keyword}
                onKeywordChange={setKeyword}
                statusFilter={statusFilter}
                onStatusFilterChange={setStatusFilter}
                onReset={handleResetFilter}
                disabled={isSubmitting}
              />
            }
          />
        </div>
        <aside
          className="min-w-0 xl:sticky xl:top-6 xl:row-span-2"
          aria-label="Thông tin và phân công phản biện"
        >
          <Card className="divide-y divide-line-subtle">
            <ReviewerAssignmentDetailCard
              selectedItem={selectedItem}
              groupDetail={groupDetailQuery.data}
              batchName={selectedBatch?.batchName}
              isLoading={groupDetailQuery.isLoading}
            />
            {groupDetailQuery.isError && (
              <div className="p-5">
                <InlineAlert
                  title="Không tải được thành viên"
                  action={
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void groupDetailQuery.refetch()}
                    >
                      Thử lại
                    </Button>
                  }
                >
                  Thông tin hồ sơ vẫn được giữ. Vui lòng thử tải lại danh sách thành viên.
                </InlineAlert>
              </div>
            )}
            <ReviewerAssignmentFormCard
              selectedItem={selectedItem}
              candidates={candidates}
              selectedReviewerId={selectedReviewerId}
              onSelectReviewerId={setSelectedReviewerId}
              onSubmitAssignment={() => assignMutation.mutate()}
              isSubmitting={isSubmitting}
              canManage={canManage}
              isLoadingCandidates={candidatesQuery.isLoading}
              candidatesError={
                candidatesQuery.isError
                  ? getApiErrorMessage(
                      candidatesQuery.error,
                      'Vui lòng thử tải lại danh sách giảng viên.',
                    )
                  : null
              }
              onRetryCandidates={() => void candidatesQuery.refetch()}
            />
          </Card>
        </aside>
        {selectedItem && (
          <div className="min-w-0 xl:col-start-1">
            <SimilarityReportPanel
              reportId={selectedItem.finalReportId}
              versionNo={selectedItem.reportVersionNo}
            />
          </div>
        )}
      </div>
    </div>
  );
}
