import { ClipboardCheck, AlertCircle, CheckCircle2 } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
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
    filteredItems,
    selectedGroup,
    setSelectedGroup,
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    reviewMutation,
    handleDownload,
    hasDownloadedFile,
    isAlreadyReviewed,
    noticeMessage,
    errorMessage,
  } = useSyllabusReview();

  const batchesQuery = useAcademicBatches();
  const currentBatch = batchesQuery.data?.find((b) => b.status === 'Open') || batchesQuery.data?.[0];
  const batchLabel = currentBatch?.batchName || 'Học kỳ hiện tại';

  const groupCodeLabel = selectedGroup?.groupCode ? `nhóm ${selectedGroup.groupCode}` : 'nhóm sinh viên';

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Cổng 1 · Thẩm định đề cương"
        description={`Khoa quyết định kết quả đề cương của ${groupCodeLabel}.`}
        icon={<ClipboardCheck />}
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {batchLabel}
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

      {noticeMessage && (
        <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-start gap-3">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Thông báo</p>
            <p className="text-xs mt-0.5 text-emerald-700">{noticeMessage}</p>
          </div>
        </div>
      )}

      <ReviewFilterBar
        searchTerm={searchTerm}
        onSearchChange={setSearchTerm}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        onApply={handleApplyFilter}
        onReset={handleResetFilter}
      />

      <ReviewSyllabusTable
        items={filteredItems}
        selectedGroupId={selectedGroup?.groupId || null}
        onSelectGroup={setSelectedGroup}
        onDownloadFile={handleDownload}
        isLoading={queueQuery.isLoading}
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
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <ReviewTopicDetailCard
            selectedGroup={selectedGroup}
            onDownloadFile={(fileId) => handleDownload(fileId, selectedGroup?.groupId)}
            disabled={isAlreadyReviewed}
            hasDownloadedFile={hasDownloadedFile}
          />
        </div>
      </div>
    </div>
  );
}
