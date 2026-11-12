import React from 'react';
import { History, Wand2, CheckCircle2, AlertCircle, Save, Send } from 'lucide-react';
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
    <div className="space-y-6 pb-10 max-w-[1440px] mx-auto selection:bg-blue-100 selection:text-blue-950">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900">
            Cấu hình đợt điểm
          </h1>
          <p className="mt-2 text-sm text-slate-600">
            Thiết lập cách tính điểm và trọng số cho từng đợt khóa luận.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={actions.handleApplyDefaultTemplateClick}
            className="rounded-lg h-10 text-xs shadow-sm"
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
              className="rounded-lg h-10 text-xs shadow-sm"
              leftIcon={<History className="size-3.5 text-slate-500" />}
            >
              Lịch sử phiên bản ({data.historyConfigs.length})
            </Button>
          )}
        </div>
      </header>

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
              Chưa có đợt khóa luận nào trong hệ thống. Vui lòng tạo đợt khóa luận trước khi tiến
              hành cấu hình điểm.
            </span>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_minmax(250px,280px)] gap-5 items-start">
        <div className="min-w-0 space-y-5">
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
          className="min-w-0 xl:sticky xl:top-6 rounded-2xl border border-slate-200 bg-white overflow-hidden shadow-sm"
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
          <div className="border-t border-slate-200 bg-slate-50/70 p-5 space-y-3">
            <p role="status" className="flex items-center gap-2 text-xs font-medium text-slate-600">
              <span
                className={`size-2 shrink-0 rounded-full ${form.isDirty ? 'bg-amber-500' : 'bg-slate-400'}`}
                aria-hidden="true"
              />
              {form.isDirty ? 'Có thay đổi chưa lưu' : 'Không có thay đổi chưa lưu'}
            </p>
            <button
              type="button"
              onClick={actions.handleSaveClick}
              disabled={actions.isSaving || data.isLoadingConfig || data.isLoadingBatches}
              className="w-full inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 shadow-sm h-11 text-white bg-primary hover:bg-blue-700 disabled:pointer-events-none disabled:opacity-50 cursor-pointer"
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
                disabled={
                  !form.isValid ||
                  actions.isSaving ||
                  actions.isPublishing ||
                  data.isLoadingConfig ||
                  data.isLoadingBatches
                }
                leftIcon={
                  actions.isPublishing ? (
                    <div className="size-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  ) : (
                    <Send className="size-4" />
                  )
                }
              >
                <span>{form.isDirty ? 'Lưu & Công bố' : 'Công bố'}</span>
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
