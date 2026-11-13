import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { OperationToast } from '@/shared/notifications';
import React from 'react';
import { History, Wand2, Save, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import {
  ScoreConfigGeneralCard,
  ScoreComponentTable,
  ScoreConfigSummaryCard,
  ScoreComponentAddDialog,
  ScoreConfigHistoryDialog,
  ScoreConfigCloneReasonDialog,
} from '../components/score-config';
import { useScoreConfig } from '../hooks/score-config';

export const ScoreConfigPage: React.FC = () => {
  const { data, form, modals, actions } = useScoreConfig();

  return (
    <div className="space-y-6 pb-10 selection:bg-blue-100 selection:text-blue-950">
      <PageHeader
        accent
        title="Cấu hình đợt điểm"
        description="Thiết lập cách tính điểm và trọng số cho từng đợt khóa luận."
      >
        <Button
          type="button"
          variant="outline"
          onClick={actions.handleApplyDefaultTemplateClick}
          className="h-10 rounded-xl px-4 shadow-2xs"
          leftIcon={<Wand2 className="size-4 text-primary" />}
        >
          Cấu hình đợt mẫu
        </Button>

        {data.historyConfigs.length > 0 && (
          <Button
            type="button"
            variant="outline"
            onClick={() => modals.setIsHistoryModalOpen(true)}
            className="h-10 rounded-xl px-4 shadow-2xs"
            leftIcon={<History className="size-4 text-slate-500" />}
          >
            Lịch sử phiên bản ({data.historyConfigs.length})
          </Button>
        )}
      </PageHeader>

      <OperationToast message={modals.notice} onConsumed={() => modals.setNotice(null)} />

      {modals.errorMessage && <InlineAlert title="Không thể hoàn tất" onDismiss={() => modals.setErrorMessage(null)}>{modals.errorMessage}</InlineAlert>}

      {data.batches.length === 0 && !data.isLoadingBatches && (
        <InlineAlert tone="warning" title="Chưa có đợt khóa luận">
              Chưa có đợt khóa luận nào trong hệ thống. Vui lòng tạo đợt khóa luận trước khi tiến
              hành cấu hình điểm.
        </InlineAlert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="min-w-0 lg:col-span-8 space-y-6">
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
            onAddComponent={() => modals.setIsAddModalOpen(true)}
            isDirty={form.isDirty}
            hasSavedConfig={Boolean(data.currentConfig)}
            isValid={form.isValid}
          />
        </div>

        <aside
          aria-label="Tóm tắt và lưu cấu hình"
          className="min-w-0 lg:col-span-4 lg:sticky lg:top-6 rounded-2xl border border-slate-200/80 bg-white overflow-hidden shadow-xs"
        >
          <ScoreConfigSummaryCard
            batch={data.currentBatch}
            calculationMethod={form.calculationMethod}
            totalWeight={form.totalWeight}
            reviewerMinimumScore={form.reviewerMinimumScore}
            versionNo={data.currentConfig?.versionNo}
            status={data.currentConfig?.status}
            isValid={form.isValid}
            isLoading={data.isLoadingConfig || data.isLoadingBatches}
          />
          <div className="border-t border-slate-200/80 bg-slate-50/70 p-5 space-y-3">
            <p role="status" className="flex items-center gap-2 text-xs font-medium text-slate-600">
              <span
                className={`size-2 shrink-0 rounded-full ${form.isDirty ? 'bg-amber-500' : 'bg-slate-400'}`}
                aria-hidden="true"
              />
              {form.isDirty ? 'Có thay đổi chưa lưu' : 'Không có thay đổi chưa lưu'}
            </p>
            <Button
              type="button"
              onClick={actions.handleSaveClick}
              disabled={actions.isSaving || data.isLoadingConfig || data.isLoadingBatches}
              className="w-full h-10 rounded-xl font-semibold shadow-2xs"
              isLoading={actions.isSaving}
              leftIcon={<Save className="size-4" />}
            >
              {data.currentConfig?.status === 'Draft'
                ? 'Lưu bản nháp'
                : data.currentConfig?.status === 'Active'
                  ? 'Sửa cấu hình'
                  : 'Lưu cấu hình'}
            </Button>

            {data.currentConfig?.status === 'Draft' && (
              <Button
                type="button"
                variant="success"
                className="w-full h-10 rounded-xl font-semibold shadow-2xs"
                onClick={actions.handlePublish}
                disabled={
                  !form.isValid ||
                  actions.isSaving ||
                  actions.isPublishing ||
                  data.isLoadingConfig ||
                  data.isLoadingBatches
                }
                isLoading={actions.isPublishing}
                leftIcon={<Send className="size-4" />}
              >
                {form.isDirty ? 'Lưu & Công bố' : 'Công bố'}
              </Button>
            )}

            {data.currentConfig?.status === 'Draft' && form.isDirty && form.isValid && (
              <p className="text-[11px] text-slate-500 text-center font-medium">
                Thay đổi chưa lưu sẽ được tự động cập nhật vào bản nháp trước khi công bố.
              </p>
            )}

            {data.currentConfig?.status === 'Draft' && !form.isValid && (
              <p className="text-[11px] text-amber-600 text-center font-medium">
                Cần đạt tổng {form.calculationMethod === 'Percentage' ? '100%' : 'hệ số > 0'} để
                công bố
              </p>
            )}
          </div>
        </aside>
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
        type="warning"
        title="Nạp lại cấu hình mẫu?"
        description="Các thay đổi chưa lưu sẽ bị mất khi nạp lại cấu hình mẫu chuẩn của UIT. Bạn có muốn tiếp tục không?"
        confirmText="Nạp lại cấu hình"
        confirmButtonClassName="bg-amber-700 hover:bg-amber-800 focus-visible:ring-amber-700/30"
        cancelText="Hủy"
      />
    </div>
  );
};
