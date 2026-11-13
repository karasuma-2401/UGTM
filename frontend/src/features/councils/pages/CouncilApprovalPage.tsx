import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';

import { PageHeader } from '@/components/ui/page-header';
import { useCouncilApproval } from '../hooks/useCouncilApproval';
import {
  CouncilApprovalFilterBar,
  CouncilApprovalTable,
  CouncilApprovalDecisionCard,
  CouncilApprovalDetailCard,
} from '../components/council-approval';

export function CouncilApprovalPage() {
  const {
    batches,
    batchFilter,
    setBatchFilter,
    keyword,
    setKeyword,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    filteredDrafts,
    pendingCount,
    selectedCouncilId,
    setSelectedCouncilId,
    selectedCouncil,
    selectedFile,
    setSelectedFile,
    customFeedback,
    setCustomFeedback,
    customDecisionNumber,
    setCustomDecisionNumber,
    noticeMessage,
    setNoticeMessage,
    errorMessage,
    canApprove,
    approveMutation,
    draftsQuery,
  } = useCouncilApproval();

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-6">
      <PageHeader
        title="Phê duyệt quyết định Hội đồng"
        description="PĐT thẩm định dự thảo do Khoa gửi."
      >
        <span className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200/80">
          {pendingCount} dự thảo chờ duyệt
        </span>
      </PageHeader>

      {errorMessage && <InlineAlert title="Không thể hoàn tất">{errorMessage}</InlineAlert>}

      <OperationToast message={noticeMessage} onConsumed={() => setNoticeMessage(null)} />

      <CouncilApprovalFilterBar
        keyword={keyword}
        onKeywordChange={setKeyword}
        batches={batches}
        batchFilter={batchFilter}
        onBatchFilterChange={setBatchFilter}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        onApply={handleApplyFilter}
        onReset={handleResetFilter}
      />

      <CouncilApprovalTable
        items={filteredDrafts}
        selectedCouncilId={selectedCouncilId}
        onSelectCouncilId={setSelectedCouncilId}
        isLoading={draftsQuery.isLoading}
      />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8">
          <CouncilApprovalDecisionCard
            selectedCouncil={selectedCouncil}
            selectedFile={selectedFile}
            onFileSelect={setSelectedFile}
            feedback={customFeedback}
            onFeedbackChange={setCustomFeedback}
            decisionNumber={customDecisionNumber}
            onDecisionNumberChange={setCustomDecisionNumber}
            onApprove={() => approveMutation.mutate()}
            isApproving={approveMutation.isPending}
            canApprove={canApprove}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6">
          <CouncilApprovalDetailCard
            selectedCouncil={selectedCouncil}
            attachedFileName={selectedFile?.name}
          />
        </div>
      </div>
    </div>
  );
}