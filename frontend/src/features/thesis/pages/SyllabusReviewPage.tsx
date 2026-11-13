import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { ClipboardCheck } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { ThesisReviewNavigation } from '../components/navigation';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useSyllabusReview } from '../hooks/useSyllabusReview';
import {
  ReviewFilterBar,
  ReviewSyllabusTable,
  ReviewActionCard,
  ReviewTopicDetailCard,
} from '../components/review';

export function SyllabusReviewPage() {
  const {
    queueQuery,
    queueErrorKind,
    queueErrorMessage,
    filteredItems,
    selectedGroup,
    setSelectedGroup,
    batchId,
    setBatchId,
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    pageNumber,
    pageSize,
    totalCount,
    hasPreviousPage,
    hasNextPage,
    handlePageChange,
    handleApplyFilter,
    handleResetFilter,
    reviewMutation,
    handleDownload,
    hasDownloadedFile,
    isAlreadyReviewed,
    noticeMessage,
    setNoticeMessage,
    errorMessage,
  } = useSyllabusReview();

  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data || [];
  const currentBatch = batches.find((b) => b.status === 'Open') || batches[0];
  const batchLabel = currentBatch?.batchName || 'Học kỳ hiện tại';

  const groupCodeLabel = selectedGroup?.groupCode ? `nhóm ${selectedGroup.groupCode}` : 'nhóm sinh viên';

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Thẩm định các cổng khóa luận"
        description={`Khoa quyết định kết quả đề cương của ${groupCodeLabel}.`}
        icon={<ClipboardCheck />}
        navigation={<ThesisReviewNavigation />}
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {batchLabel}
        </span>
      </PageHeader>

      {errorMessage && <InlineAlert title="Không thể hoàn tất">{errorMessage}</InlineAlert>}

      <OperationToast message={noticeMessage} onConsumed={() => setNoticeMessage(null)} />

      <ReviewFilterBar
        searchTerm={searchTerm}
        onSearchChange={setSearchTerm}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        batchId={batchId}
        onBatchChange={setBatchId}
        batches={batches}
        onApply={handleApplyFilter}
        onReset={handleResetFilter}
      />

      <ReviewSyllabusTable
        items={filteredItems}
        selectedGroupId={selectedGroup?.groupId || null}
        onSelectGroup={setSelectedGroup}
        onDownloadFile={handleDownload}
        isLoading={queueQuery.isLoading}
        errorKind={queueErrorKind}
        errorMessage={queueErrorMessage}
        onRetry={() => void queueQuery.refetch()}
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        hasPreviousPage={hasPreviousPage}
        hasNextPage={hasNextPage}
        onPageChange={handlePageChange}
      />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8">
          <ReviewActionCard
            selectedGroup={selectedGroup}
            onSubmitReview={async (decision, feedback) => {
              if (selectedGroup) {
                await reviewMutation.mutateAsync({
                  groupId: selectedGroup.groupId,
                  decision,
                  feedback,
                });
              }
            }}
            isSubmitting={reviewMutation.isPending}
            disabled={isAlreadyReviewed}
            hasDownloadedFile={hasDownloadedFile}
            onDownloadFile={() => {
              if (selectedGroup?.fileId) {
                void handleDownload(selectedGroup.fileId, selectedGroup.groupId);
              }
            }}
            gateName="đề cương"
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <ReviewTopicDetailCard
            selectedGroup={selectedGroup}
            onDownloadFile={(fileId) => handleDownload(fileId, selectedGroup?.groupId)}
            disabled={isAlreadyReviewed}
            hasDownloadedFile={hasDownloadedFile}
            gateTitle="Đề cương"
          />
        </div>
      </div>
    </div>
  );
}
