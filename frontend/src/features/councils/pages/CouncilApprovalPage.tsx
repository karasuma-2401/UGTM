import { AlertCircle, CheckCircle2 } from 'lucide-react';
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