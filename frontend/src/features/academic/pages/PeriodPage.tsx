import React from 'react';
import { Plus, RefreshCw, AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
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

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">
            Khóa luận / Phòng Đào tạo
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Khởi tạo đợt & deadline
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <select
            value={data.selectedBatchId}
            onChange={(e) => data.setSelectedBatchId(e.target.value)}
            disabled={data.isLoadingBatches}
            className="h-9 rounded-md border border-slate-200 bg-white px-3 text-xs font-medium text-slate-800 shadow-2xs focus:outline-hidden focus:ring-2 focus:ring-[#2054A5]"
          >
            <option value="new">+ Khởi tạo đợt mới</option>
            {data.batches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.batchName} ({b.academicYear} - HK{b.semester}) [{b.status}]
              </option>
            ))}
          </select>

          {permissions.canManageBatch && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => data.setSelectedBatchId('new')}
              className="h-9 gap-1.5 text-xs font-medium"
            >
              <Plus className="size-3.5" />
              Đợt mới
            </Button>
          )}

          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={actions.reloadCurrentBatch}
            disabled={data.isLoadingDetail}
            className="size-9 text-slate-500 hover:text-slate-800"
            title="Tải lại dữ liệu"
          >
            <RefreshCw className={`size-4 ${data.isLoadingDetail ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </div>

      {modals.notice && (
        <div className="flex items-center gap-2.5 rounded-xl border border-emerald-200 bg-emerald-50/80 p-4 text-sm font-medium text-emerald-800 shadow-2xs">
          <CheckCircle2 className="size-5 shrink-0 text-emerald-600" />
          <span>{modals.notice}</span>
        </div>
      )}

      {modals.errorMessage && (
        <div className="flex items-center gap-2.5 rounded-xl border border-rose-200 bg-rose-50/80 p-4 text-sm font-medium text-rose-800 shadow-2xs">
          <AlertTriangle className="size-5 shrink-0 text-rose-600" />
          <span>{modals.errorMessage}</span>
        </div>
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