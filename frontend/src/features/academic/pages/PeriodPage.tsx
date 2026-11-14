import React, { useMemo } from 'react';
import { Plus, RefreshCw, RotateCcw } from 'lucide-react';
import { OperationToast } from '@/shared/notifications';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { PageHeader } from '@/components/ui/page-header';
import {
  PeriodStagesCard,
  PeriodInfoCard,
  PeriodDeadlinesCard,
  PeriodSummaryCard,
  PeriodActionBar,
  PeriodReviewGateDialog,
} from '../components/periods';
import { ScoreConfigCloneReasonDialog } from '../components/score-config/ScoreConfigCloneReasonDialog';
import { usePeriod } from '../hooks/periods';

export const PeriodPage: React.FC = () => {
  const { data, form, modals, actions, permissions } = usePeriod();

  const batchOptions = useMemo(
    () => [
      { value: 'new', label: '+ Khởi tạo đợt mới' },
      ...data.batches.map((b) => ({
        value: b.id,
        label: `${b.batchName} (${b.academicYear} - HK${b.semester})`,
      })),
    ],
    [data.batches],
  );

  return (
    <div className="space-y-6 pb-12 animate-in fade-in-50 duration-300">
      <PageHeader title="Khởi tạo đợt & deadline">
        <div className="w-full sm:w-80 md:w-96">
          <Select
            options={batchOptions}
            value={data.selectedBatchId}
            onChange={(val) => data.setSelectedBatchId(String(val ?? 'new'))}
            disabled={data.isLoadingBatches}
            size="lg"
            triggerClassName="h-10 rounded-xl border-slate-200/90 bg-white text-xs font-semibold text-slate-800 shadow-2xs hover:border-slate-300 transition-all select-none"
            contentClassName="w-max min-w-full sm:min-w-[440px] max-w-[90vw] sm:right-0 sm:left-auto p-1.5 rounded-xl border-slate-200/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-slate-700 hover:bg-slate-50 transition-colors"
          />
        </div>

        {permissions.canManageBatch && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => data.setSelectedBatchId('new')}
            className="h-10 gap-1.5 rounded-xl border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-2xs hover:bg-slate-50 hover:text-slate-900 active:scale-[0.98] transition-all"
          >
            <Plus className="size-3.5" />
            Đợt mới
          </Button>
        )}

        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={actions.reloadCurrentBatch}
          disabled={data.isLoadingDetail}
          className="size-10 rounded-xl border-slate-200 bg-white text-slate-500 hover:text-slate-900 shadow-2xs active:scale-[0.98] transition-all"
          title="Tải lại dữ liệu"
        >
          <RefreshCw className={`size-4 ${data.isLoadingDetail ? 'animate-spin' : ''}`} />
        </Button>
      </PageHeader>

      <OperationToast message={modals.notice} onConsumed={() => modals.setNotice(null)} />

      {modals.errorMessage && (
        <InlineAlert
          onDismiss={() => modals.setErrorMessage(null)}
          action={
            actions.failedGateUpdates.length > 0 ? (
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={actions.isSaving || data.isLoadingDetail}
                onClick={actions.handleRetryFailedGates}
                className="h-8 text-xs border-rose-300 bg-white text-rose-700 hover:bg-rose-100 shrink-0"
              >
                <RotateCcw className="size-3.5 mr-1.5" />
                Thử lại cập nhật cổng ({actions.failedGateUpdates.length})
              </Button>
            ) : undefined
          }
        >
          {modals.errorMessage}
        </InlineAlert>
      )}

      {/* Row 1: Core Form + Summary Widget */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-6">
          <PeriodInfoCard
            academicYear={form.academicYear}
            semester={form.semester}
            batchName={form.batchName}
            startDate={form.startDate}
            canEdit={permissions.canEdit}
            onAcademicYearChange={form.setAcademicYear}
            onSemesterChange={form.setSemester}
            onBatchNameChange={form.setBatchName}
            onStartDateChange={form.setStartDate}
          />

          <PeriodDeadlinesCard
            syllabusDeadline={form.syllabusDeadline}
            midtermDeadline={form.midtermDeadline}
            finalEvaluationDeadline={form.finalEvaluationDeadline}
            defenseDeadline={form.defenseDeadline}
            syllabusMinDate={form.computedStages[1]?.startDate}
            midtermMinDate={form.computedStages[3]?.startDate}
            finalEvaluationMinDate={form.computedStages[4]?.startDate}
            defenseMinDate={form.computedStages[5]?.startDate}
            canEdit={permissions.canEdit}
            onSyllabusDeadlineChange={form.handleSyllabusDeadlineChange}
            onMidtermDeadlineChange={form.handleMidtermDeadlineChange}
            onFinalEvaluationDeadlineChange={form.handleFinalEvaluationDeadlineChange}
            onDefenseDeadlineChange={form.handleDefenseDeadlineChange}
            onOpenGateDetails={() => modals.setIsGateDetailsOpen(true)}
          />
        </div>

        <div>
          <PeriodSummaryCard
            academicYear={form.academicYear}
            semester={form.semester}
            batchName={form.batchName}
            status={data.currentBatch?.status || 'Draft'}
            totalWeeks={form.totalWeeks}
            canEdit={permissions.canEdit}
            onOpenBatch={actions.handleOpenBatch}
            isOpenLoading={actions.isOpenLoading}
          />
        </div>
      </div>

      {/* Row 2: 7 Stages Table & Interactive Timeline */}
      <PeriodStagesCard
        computedStages={form.computedStages}
        canEdit={permissions.canEdit}
        onWeeksChange={form.handleStageWeeksChange}
        onResetDefaultWeeks={form.handleResetDefaultWeeks}
        totalWeeks={form.totalWeeks}
        syllabusDeadline={form.syllabusDeadline}
        midtermDeadline={form.midtermDeadline}
        finalEvaluationDeadline={form.finalEvaluationDeadline}
        defenseDeadline={form.defenseDeadline}
      />

      {/* Floating Action Bar */}
      <PeriodActionBar
        canEdit={permissions.canEdit}
        isSaving={actions.isSaving}
        isDisabled={data.isLoadingDetail || !data.isSelectionSynced}
        onSave={actions.handleSaveClick}
      />

      {/* Dialogs */}
      <PeriodReviewGateDialog
        open={modals.isGateDetailsOpen}
        onClose={() => modals.setIsGateDetailsOpen(false)}
        canEdit={permissions.canEdit}
        syllabusGate={data.syllabusGate}
        midtermGate={data.midtermGate}
        defaultSyllabusDeadline={form.syllabusDeadline}
        defaultMidtermDeadline={form.midtermDeadline}
        onSaveGates={actions.handleSaveGates}
      />

      <ScoreConfigCloneReasonDialog
        open={modals.isChangeReasonOpen}
        onClose={() => modals.setIsChangeReasonOpen(false)}
        onConfirm={actions.handleConfirmChangeReason}
      />
    </div>
  );
};
