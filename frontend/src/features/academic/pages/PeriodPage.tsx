import { OperationToast } from '@/shared/notifications';
import React, { useMemo } from 'react';
import { Plus, RefreshCw, CalendarDays, RotateCcw } from 'lucide-react';
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
import { StudentImportDialog } from '@/features/identity';
import { ScoreConfigCloneReasonDialog } from '../components/score-config/ScoreConfigCloneReasonDialog';
import { usePeriod } from '../hooks/periods';

export const PeriodPage: React.FC = () => {
  const { data, form, modals, actions, permissions } = usePeriod();

  const batchOptions = useMemo(
    () => [
      { value: 'new', label: '+ Khởi tạo đợt mới' },
      ...data.batches.map((b) => ({
        value: b.id,
        label: `${b.batchName} (${b.academicYear} - HK${b.semester}) [${b.status}]`,
      })),
    ],
    [data.batches],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Khởi tạo đợt & deadline"
        icon={<CalendarDays />}
      >
        <div className="w-72 sm:w-80">
          <Select
            options={batchOptions}
            value={data.selectedBatchId}
            onChange={(val) => data.setSelectedBatchId(String(val ?? 'new'))}
            disabled={data.isLoadingBatches}
            size="lg"
            triggerClassName="h-10 rounded-lg border-slate-200 bg-white text-xs font-medium text-slate-800 shadow-sm"
          />
        </div>

        {permissions.canManageBatch && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => data.setSelectedBatchId('new')}
            className="h-10 gap-1.5 text-xs font-medium shadow-sm"
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
          className="size-10 text-slate-500 hover:text-slate-800 shadow-sm"
          title="Tải lại dữ liệu"
        >
          <RefreshCw className={`size-4 ${data.isLoadingDetail ? 'animate-spin' : ''}`} />
        </Button>
      </PageHeader>

      <OperationToast message={modals.notice} onConsumed={() => modals.setNotice(null)} />

      {modals.errorMessage && (
        <InlineAlert action={actions.failedGateUpdates.length > 0 ? (
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
          ) : undefined}>
          {modals.errorMessage}
        </InlineAlert>
      )}

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

        <div className="space-y-6">
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

      <PeriodStagesCard
        computedStages={form.computedStages}
        canEdit={permissions.canEdit}
        onWeeksChange={form.handleStageWeeksChange}
        onResetDefaultWeeks={form.handleResetDefaultWeeks}
        totalWeeks={form.totalWeeks}
      />

      <PeriodActionBar
        canEdit={permissions.canEdit}
        isSaving={actions.isSaving}
        isDisabled={data.isLoadingDetail || !data.isSelectionSynced}
        onSave={actions.handleSaveClick}
        onImportUsers={() => modals.setIsImportOpen(true)}
      />

      <PeriodReviewGateDialog
        open={modals.isGateDetailsOpen}
        onClose={() => modals.setIsGateDetailsOpen(false)}
        canEdit={permissions.canEdit}
        syllabusGate={data.syllabusGate}
        midtermGate={data.midtermGate}
        onSaveGates={actions.handleSaveGates}
      />

      {modals.isImportOpen && (
        <StudentImportDialog
          onClose={() => modals.setIsImportOpen(false)}
          onImported={async (count: number) => {
            modals.setIsImportOpen(false);
            modals.setNotice(`Import thành công ${count} tài khoản sinh viên.`);
          }}
        />
      )}

      <ScoreConfigCloneReasonDialog
        open={modals.isChangeReasonOpen}
        onClose={() => modals.setIsChangeReasonOpen(false)}
        onConfirm={actions.handleConfirmChangeReason}
      />
    </div>
  );
};
