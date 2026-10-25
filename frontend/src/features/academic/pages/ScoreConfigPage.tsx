import React from 'react';
import { History, Wand2, CheckCircle2, AlertCircle, Save, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import {
  ScoreConfigGeneralCard,
  ScoreComponentTable,
  ScoreConfigValidationCard,
  ScoreConfigSummaryCard,
  ScoreConfigActionBar,
  ScoreComponentAddDialog,
  ScoreConfigHistoryDialog,
  ScoreConfigCloneReasonDialog,
} from '../components/score-config';
import { useScoreConfig } from '../hooks/score-config';

export const ScoreConfigPage: React.FC = () => {
  const { data, form, modals, actions } = useScoreConfig();

  return (
    <div className="space-y-6 pb-12 max-w-7xl mx-auto">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold text-slate-900 tracking-tight">
            Cấu hình điểm theo đợt
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 mt-1">
            Thiết lập phương thức tính điểm, trọng số và điều kiện áp dụng cho từng đầu điểm.
          </p>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={actions.handleApplyDefaultTemplateClick}
            className="rounded-lg h-9 text-xs"
            leftIcon={<Wand2 className="size-3.5 text-primary" />}
          >
            Cấu hình đợt mẫu
          </Button>

          {data.historyConfigs.length > 0 && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => modals.setIsHistoryModalOpen(true)}
              className="rounded-lg h-9 text-xs"
              leftIcon={<History className="size-3.5 text-slate-500" />}
            >
              Lịch sử phiên bản ({data.historyConfigs.length})
            </Button>
          )}
        </div>
      </div>

      {modals.notice && (
        <div
          role="status"
          className="rounded-xl bg-emerald-50 border border-emerald-200 p-4 text-xs sm:text-sm text-emerald-800 flex items-center justify-between shadow-xs transition-all animate-in fade-in"
        >
          <div className="flex items-center gap-2.5">
            <CheckCircle2 className="size-5 text-emerald-600 shrink-0" />
            <span className="font-medium">{modals.notice}</span>
          </div>
          <button
            type="button"
            onClick={() => modals.setNotice(null)}
            className="text-emerald-700 hover:text-emerald-950 font-bold ml-3 text-xs underline cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      {modals.errorMessage && (
        <div
          role="alert"
          className="rounded-xl bg-rose-50 border border-rose-200 p-4 text-xs sm:text-sm text-rose-800 flex items-center justify-between shadow-xs transition-all animate-in fade-in"
        >
          <div className="flex items-center gap-2.5">
            <AlertCircle className="size-5 text-rose-600 shrink-0" />
            <span className="font-medium">{modals.errorMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => modals.setErrorMessage(null)}
            className="text-rose-700 hover:text-rose-950 font-bold ml-3 text-xs underline cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      {data.batches.length === 0 && !data.isLoadingBatches && (
        <div
          role="alert"
          className="rounded-xl bg-amber-50 border border-amber-200 p-4 text-xs sm:text-sm text-amber-800 flex items-center justify-between shadow-xs transition-all animate-in fade-in"
        >
          <div className="flex items-center gap-2.5">
            <AlertCircle className="size-5 text-amber-600 shrink-0" />
            <span className="font-medium">
              Chưa có đợt khóa luận nào trong hệ thống. Vui lòng tạo đợt khóa luận trước khi tiến hành cấu hình điểm.
            </span>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-8 space-y-6">
          <ScoreConfigGeneralCard
            batches={data.batches}
            selectedBatchId={data.selectedBatchId}
            onBatchChange={data.setSelectedBatchId}
            calculationMethod={form.calculationMethod}
            onCalculationMethodChange={form.handleSwitchCalculationMethod}
            reviewerMinimumScore={form.reviewerMinimumScore}
            onReviewerMinimumScoreChange={form.handleReviewerMinScoreChange}
            onAddOptionalComponent={form.handleAddOptionalComponent}
            isReadOnly={data.isLoadingConfig || data.isLoadingBatches}
            isLoadingBatches={data.isLoadingBatches}
          />

          <ScoreComponentTable
            components={form.components}
            calculationMethod={form.calculationMethod}
            onChangeComponents={form.setComponents}
            isReadOnly={data.isLoadingConfig || data.isLoadingBatches}
          />

          <ScoreConfigActionBar
            calculationMethod={form.calculationMethod}
            onSwitchCalculationMethod={form.handleSwitchCalculationMethod}
            onOpenAddComponentModal={() => modals.setIsAddModalOpen(true)}
            isReadOnly={data.isLoadingConfig || data.isLoadingBatches}
          />

          <ScoreConfigValidationCard
            calculationMethod={form.calculationMethod}
            totalWeight={form.totalWeight}
            componentsCount={form.components.length}
          />
        </div>

        <div className="lg:col-span-4 sticky top-6 space-y-4">
          <ScoreConfigSummaryCard
            batch={data.currentBatch}
            calculationMethod={form.calculationMethod}
            totalWeight={form.totalWeight}
            reviewerMinimumScore={form.reviewerMinimumScore}
            versionNo={data.currentConfig?.versionNo}
            status={data.currentConfig?.status}
          />

          <button
            type="button"
            onClick={actions.handleSaveClick}
            disabled={actions.isSaving || data.isLoadingConfig || data.isLoadingBatches}
            className="w-full inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary shadow-sm h-11 text-white bg-gradient-to-r from-[#2054A5] to-[#FA7921] hover:opacity-95 active:scale-[0.99] disabled:pointer-events-none disabled:opacity-50 cursor-pointer"
          >
            {actions.isSaving ? (
              <div className="size-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
            ) : (
              <Save className="size-4" />
            )}
            <span>
              {data.currentConfig?.status === 'Draft'
                ? 'Lưu bản nháp'
                : data.currentConfig?.status === 'Active'
                ? 'Sửa cấu hình'
                : 'Lưu cấu hình'}
            </span>
          </button>

          {data.currentConfig?.status === 'Draft' && (
            <Button
              type="button"
              variant="default"
              className="w-full rounded-lg h-11 bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm font-semibold text-sm cursor-pointer disabled:pointer-events-none disabled:opacity-50"
              onClick={actions.handlePublish}
              disabled={!form.isValid || actions.isPublishing || data.isLoadingConfig || data.isLoadingBatches}
              leftIcon={
                actions.isPublishing ? (
                  <div className="size-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                ) : (
                  <Send className="size-4" />
                )
              }
            >
              <span>Công bố</span>
            </Button>
          )}

          {data.currentConfig?.status === 'Draft' && !form.isValid && (
            <p className="text-[11px] text-amber-600 text-center font-medium">
              Cần đạt tổng {form.calculationMethod === 'Percentage' ? '100%' : 'hệ số > 0'} để công bố
            </p>
          )}
        </div>
      </div>

      <ScoreComponentAddDialog
        open={modals.isAddModalOpen}
        onClose={() => modals.setIsAddModalOpen(false)}
        calculationMethod={form.calculationMethod}
        existingCodes={form.components.map((c) => c.componentCode)}
        nextOrderIndex={form.components.length + 1}
        onAdd={(newComp) => {
          form.setComponents((prev) => [...prev, newComp]);
          modals.setNotice(`Đã thêm đầu điểm "${newComp.componentName}".`);
        }}
      />

      <ScoreConfigHistoryDialog
        open={modals.isHistoryModalOpen}
        onClose={() => modals.setIsHistoryModalOpen(false)}
        history={data.historyConfigs}
        onSelectVersion={actions.handleSelectHistoryVersion}
      />

      <ScoreConfigCloneReasonDialog
        open={modals.isCloneReasonModalOpen}
        onClose={() => modals.setIsCloneReasonModalOpen(false)}
        isSubmitting={actions.isSaving}
        onConfirm={(reason) => {
          modals.setIsCloneReasonModalOpen(false);
          actions.executeSaveDraft(reason);
        }}
      />

      <ConfirmDialog
        open={modals.isResetConfirmModalOpen}
        onClose={() => modals.setIsResetConfirmModalOpen(false)}
        onConfirm={actions.applyDefaultTemplate}
        type="brand"
        title="Cảnh báo!"
        description="Cấu hình điểm hiện tại đã có sự thay đổi. Bạn có chắc chắn muốn nạp lại cấu hình mẫu chuẩn của UIT không? Các thiết lập vừa chỉnh sửa chưa lưu sẽ bị mất."
        confirmText="Đồng ý"
        cancelText="Hủy"
      />
    </div>
  );
};