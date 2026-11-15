import { useEffect, useMemo } from 'react';
import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  ChevronRight,
  FileCheck2,
  Layers,
  Lock,
  Printer,
  RefreshCw,
  Unlock,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import { useCouncilMinutes } from '../hooks/useCouncilMinutes';
import {
  CouncilMinuteAuditDock,
  CouncilMinuteGroupDrawer,
  CouncilMinuteSheet,
  LockMinuteDialog,
  UnlockMinuteDialog,
} from '../components/minutes';
import {
  formatDefenseDateVi,
  getMinuteLockStatusMeta,
} from '../utils/councilMinuteFormatting';

function WorkspaceLoadingSkeleton() {
  return (
    <div
      aria-busy="true"
      aria-label="Đang tải biên bản hội đồng"
      className="grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_340px] items-start"
    >
      <div className="rounded-2xl border border-slate-200/80 bg-white overflow-hidden">
        <Skeleton className="h-1 w-full rounded-none" />
        <div className="p-5 sm:p-7 space-y-6">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 border-b border-slate-100 pb-5">
            <div className="space-y-2 flex-1">
              <Skeleton className="h-3.5 w-64" />
              <Skeleton className="h-6 w-80 max-w-full" />
              <Skeleton className="h-4 w-56" />
            </div>
            <Skeleton className="h-7 w-28 rounded-full" />
          </div>
          <div className="rounded-xl border border-slate-200/70 bg-slate-50/60 p-4 space-y-4">
            <div className="space-y-1.5">
              <Skeleton className="h-5 w-4/5" />
              <Skeleton className="h-3.5 w-3/5" />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 border-t border-slate-200/60 pt-3.5">
              <div className="space-y-2">
                <Skeleton className="h-4 w-36" />
                <Skeleton className="h-6 w-48" />
                <Skeleton className="h-6 w-44" />
              </div>
              <div className="space-y-2">
                <Skeleton className="h-6 w-full" />
                <Skeleton className="h-6 w-full" />
                <Skeleton className="h-6 w-5/6" />
                <Skeleton className="h-6 w-11/12" />
              </div>
            </div>
          </div>
          <Skeleton className="h-56 w-full rounded-xl" />
          <Skeleton className="h-32 w-full rounded-xl" />
        </div>
      </div>

      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-200/80 bg-white p-5 space-y-4">
          <Skeleton className="h-5 w-40" />
          <div className="grid grid-cols-2 gap-3">
            <Skeleton className="h-20 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
          </div>
          <Skeleton className="h-40 w-full rounded-xl" />
        </div>
        <div className="rounded-2xl border border-slate-200/80 bg-white p-5 space-y-3">
          <Skeleton className="h-5 w-36" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
          <Skeleton className="h-10 w-full rounded-xl mt-2" />
        </div>
      </div>
    </div>
  );
}

export function CouncilMinutesPage() {
  const {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    isLoadingBatches,

    accessibleAssignments,
    filteredQueue,
    sameCouncilAssignments,
    activeOverviewRow,
    queueStats,
    queueSearch,
    setQueueSearch,
    queueFilter,
    setQueueFilter,
    isGroupDrawerOpen,
    setIsGroupDrawerOpen,

    isLoadingList,
    listError,
    reloadList,

    selectedAssignmentId,
    handleSelectAssignment,
    pendingSwitchId,
    cancelPendingSwitch,
    confirmDiscardAndSwitch,

    workspace,
    discrepancyDetail,
    schedule,
    currentGroupInSchedule,
    isLoadingWorkspace,
    workspaceError,
    reloadWorkspace,

    effectiveScoreItems,
    effectiveDiscrepancy,
    effectiveCalculation,
    selectableExclusionTargets,

    secretaryMarking,
    targetComponentCode,
    studentFeedback,
    setStudentFeedback,
    exclusionReason,
    setExclusionReason,
    requiresExclusionReason,
    changesExclusion,
    isDirty,
    isPreviewing,
    previewError,

    isLocked,
    isCouncilActive,
    canEditMinute,
    canLockMinute,
    canUnlockMinute,

    handleChangeSecretaryMarking,
    handleChangeTargetComponent,
    handleSaveMinute,
    isSaving,

    isLockModalOpen,
    setIsLockModalOpen,
    isLocking,
    lockModalError,
    handleConfirmLock,

    isUnlockModalOpen,
    setIsUnlockModalOpen,
    isUnlocking,
    unlockModalError,
    handleConfirmUnlock,

    isPrintPreview,
    setIsPrintPreview,
  } = useCouncilMinutes();

  const batchOptions = useMemo(
    () =>
      batches.map((b) => ({
        value: b.id,
        label: b.status === 'Open' ? `${b.batchName} (Đang mở)` : b.batchName,
      })),
    [batches],
  );

  const pendingRow = useMemo(
    () => accessibleAssignments.find((item) => item.assignmentId === pendingSwitchId) ?? null,
    [accessibleAssignments, pendingSwitchId],
  );

  const handleTriggerPrint = () => {
    window.print();
  };

  useEffect(() => {
    if (!isPrintPreview) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (
        event.key === 'Escape' &&
        !isGroupDrawerOpen &&
        !isLockModalOpen &&
        !isUnlockModalOpen &&
        !pendingSwitchId
      ) {
        setIsPrintPreview(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    isPrintPreview,
    isGroupDrawerOpen,
    isLockModalOpen,
    isUnlockModalOpen,
    pendingSwitchId,
    setIsPrintPreview,
  ]);

  return (
    <div className="space-y-6 pb-10">
      {/* Header + Actions (hidden when printing) */}
      <div className="print:hidden space-y-4">
        <PageHeader
          accent
          title="Biên bản hội đồng"
          description="Biên bản điện tử xử lý sai biệt điểm, đối chiếu công thức tổng hợp và khóa kết quả bảo vệ khóa luận."
        >
          <div className="flex flex-wrap items-center gap-2.5">
            <div className="w-56 sm:w-64">
              <Select
                size="sm"
                aria-label="Chọn đợt khóa luận"
                value={selectedBatchId}
                onChange={setSelectedBatchId}
                disabled={isLoadingBatches || batches.length === 0}
                options={
                  batchOptions.length > 0
                    ? batchOptions
                    : [{ value: '', label: 'Chưa có đợt khóa luận' }]
                }
              />
            </div>

            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => setIsGroupDrawerOpen(true)}
              disabled={isLoadingList}
              leftIcon={<Layers className="size-4 text-primary" />}
              className="h-9 rounded-xl px-3.5 text-xs font-semibold"
            >
              Danh sách biên bản
              <span className="ml-1 inline-flex items-center rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-bold tabular-nums text-primary">
                {accessibleAssignments.length}
              </span>
            </Button>
          </div>
        </PageHeader>

        {/* Quick Switcher Strip for Groups inside Active Council */}
        {!isPrintPreview && sameCouncilAssignments.length > 0 && activeOverviewRow && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200/80 bg-white px-4 py-2.5 shadow-[0_1px_2px_rgba(15,23,42,0.03)]">
            <div className="flex flex-wrap items-center gap-2 min-w-0">
              <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-800">
                <Calendar className="size-3.5 text-slate-400" aria-hidden="true" />
                {activeOverviewRow.councilCode}
              </span>
              <span className="text-slate-300" aria-hidden="true">
                ·
              </span>
              <span className="text-xs text-slate-500">
                {formatDefenseDateVi(activeOverviewRow.defenseDate)} · Phòng{' '}
                {activeOverviewRow.room}
              </span>

              <span className="hidden sm:inline text-slate-300" aria-hidden="true">
                |
              </span>

              <span className="text-[11px] font-medium text-slate-500">
                Nhóm trong HĐ:
              </span>

              <div className="flex flex-wrap items-center gap-1.5">
                {sameCouncilAssignments.map((row) => {
                  const rowAssignmentId = row.assignmentId ?? '';
                  const active = rowAssignmentId === selectedAssignmentId;
                  const statusMeta = getMinuteLockStatusMeta(row.minuteLockStatus);
                  const dotClass = active
                    ? 'bg-white/90'
                    : statusMeta.tone === 'success'
                      ? 'bg-emerald-500'
                      : statusMeta.tone === 'warning'
                        ? 'bg-amber-500'
                        : statusMeta.tone === 'error'
                          ? 'bg-rose-500'
                          : statusMeta.tone === 'info'
                            ? 'bg-blue-500'
                            : 'bg-slate-400';

                  return (
                    <button
                      key={rowAssignmentId || row.id}
                      type="button"
                      title={`${row.groupCode} · ${statusMeta.label}${row.topicTitle ? ` — ${row.topicTitle}` : ''}`}
                      aria-label={`Nhóm ${row.groupCode}: ${statusMeta.label}`}
                      aria-current={active ? 'true' : undefined}
                      onClick={() => {
                        if (row.assignmentId && row.assignmentId !== selectedAssignmentId) {
                          handleSelectAssignment(row.assignmentId);
                        }
                      }}
                      className={cn(
                        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium',
                        active
                          ? 'border-primary bg-primary text-white shadow-xs'
                          : 'border-slate-200 bg-slate-50 text-slate-700 hover:border-slate-300 hover:bg-slate-100/80',
                      )}
                    >
                      {row.minuteLockStatus === 'Locked' ? (
                        <Lock className="size-3 shrink-0 opacity-85" aria-hidden="true" />
                      ) : row.minuteLockStatus === 'UnlockedByOverride' ? (
                        <Unlock className="size-3 shrink-0 opacity-85" aria-hidden="true" />
                      ) : row.profileStatus === 'Discrepancy' ? (
                        <AlertTriangle className="size-3 shrink-0 opacity-85" aria-hidden="true" />
                      ) : null}
                      <span>{row.groupCode}</span>
                      <span
                        className={cn('size-1.5 shrink-0 rounded-full', dotClass)}
                        aria-hidden="true"
                      />
                    </button>
                  );
                })}
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-3 shrink-0">
              {isDirty ? (
                <span className="inline-flex items-center rounded-full border border-amber-200 bg-amber-50 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                  Có thay đổi chưa lưu
                </span>
              ) : (
                <StatusBadge tone={getMinuteLockStatusMeta(activeOverviewRow.minuteLockStatus).tone}>
                  {activeOverviewRow.groupCode}:{' '}
                  {getMinuteLockStatusMeta(activeOverviewRow.minuteLockStatus).label}
                </StatusBadge>
              )}
              <span className="hidden sm:inline-block h-4 w-px bg-slate-200" aria-hidden="true" />
              <button
                type="button"
                onClick={() => setIsGroupDrawerOpen(true)}
                className="inline-flex items-center gap-1 rounded-md px-1.5 py-1 text-xs font-semibold text-primary hover:bg-primary/5 hover:underline whitespace-nowrap"
              >
                Tất cả biên bản
                <ChevronRight className="size-3.5" aria-hidden="true" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Main Content Area */}
      {listError ? (
        <InlineAlert
          tone="error"
          title="Không thể tải danh sách hội đồng"
          action={
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={reloadList}
              leftIcon={<RefreshCw className="size-3.5" />}
            >
              Thử lại
            </Button>
          }
        >
          {listError}
        </InlineAlert>
      ) : isLoadingList && accessibleAssignments.length === 0 ? (
        <WorkspaceLoadingSkeleton />
      ) : accessibleAssignments.length === 0 ? (
        <section className="rounded-2xl border border-slate-200/80 bg-white px-6 py-14 text-center shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
          <div className="mx-auto flex size-12 items-center justify-center rounded-2xl bg-slate-100 text-slate-500">
            <FileCheck2 className="size-6" aria-hidden="true" />
          </div>
          <h2 className="mt-4 text-base font-semibold text-slate-900">
            Chưa có biên bản hội đồng trong đợt này
          </h2>
          <p className="mx-auto mt-1.5 max-w-md text-xs sm:text-sm leading-relaxed text-slate-500">
            Hệ thống chỉ hiển thị các nhóm bảo vệ thuộc hội đồng bạn tham gia hoặc thuộc phạm vi
            quản lý của bạn trong đợt khóa luận đang chọn.
          </p>
          <div className="mt-5 flex flex-wrap items-center justify-center gap-2.5">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={reloadList}
              leftIcon={<RefreshCw className="size-3.5" />}
            >
              Tải lại danh sách
            </Button>
          </div>
        </section>
      ) : workspaceError ? (
        <InlineAlert
          tone="error"
          title="Không thể tải biên bản của nhóm đang chọn"
          action={
            <div className="flex items-center gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => setIsGroupDrawerOpen(true)}
              >
                Đổi nhóm khác
              </Button>
              <Button
                type="button"
                size="sm"
                variant="default"
                onClick={reloadWorkspace}
                leftIcon={<RefreshCw className="size-3.5" />}
              >
                Thử lại
              </Button>
            </div>
          }
        >
          {workspaceError}
        </InlineAlert>
      ) : isLoadingWorkspace ||
        !workspace ||
        (Boolean(selectedAssignmentId) &&
          workspace.assignmentInfo.assignmentId !== selectedAssignmentId) ? (
        <WorkspaceLoadingSkeleton />
      ) : effectiveDiscrepancy && effectiveCalculation ? (
        <div
          className={cn(
            isPrintPreview
              ? 'mx-auto max-w-[860px] space-y-4'
              : 'grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_340px] items-start',
          )}
        >
          {isPrintPreview && (
            <div className="sticky top-20 z-20 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200/90 bg-white/95 px-3.5 py-2.5 shadow-[0_2px_10px_rgba(15,23,42,0.06)] backdrop-blur-md print:hidden">
              <div className="flex items-center gap-2.5 min-w-0">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setIsPrintPreview(false)}
                  leftIcon={<ArrowLeft className="size-3.5" />}
                  title="Thoát xem trước (Esc)"
                  className="h-8 rounded-xl px-2.5 text-xs font-semibold text-slate-700 hover:bg-slate-100 hover:text-slate-900 shrink-0"
                >
                  Thoát xem trước
                </Button>
                <span className="hidden sm:inline-block h-4 w-px bg-slate-200 shrink-0" aria-hidden="true" />
                <span className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-0.5 text-[11px] font-semibold text-blue-700 shrink-0">
                  <Printer className="size-3" aria-hidden="true" />
                  Xem trước A4
                </span>
                <span className="hidden md:inline truncate text-xs text-slate-500">
                  Kiểm tra bố cục, điểm số và chữ ký trước khi xuất PDF
                </span>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleTriggerPrint}
                leftIcon={<Printer className="size-3.5" />}
                className="h-8 rounded-xl border-red-600 bg-red-600 px-3.5 text-xs font-semibold text-white hover:bg-red-700 hover:border-red-700 hover:text-white shrink-0"
              >
                In / Lưu PDF
              </Button>
            </div>
          )}

          <div className="min-w-0">
            <CouncilMinuteSheet
              workspace={workspace}
              overviewRow={activeOverviewRow}
              schedule={schedule}
              currentGroup={currentGroupInSchedule}
              discrepancyDetail={discrepancyDetail}
              scoreItems={effectiveScoreItems}
              discrepancySummary={effectiveDiscrepancy}
              calculationPreview={effectiveCalculation}
              selectableTargets={selectableExclusionTargets}
              secretaryMarking={secretaryMarking}
              targetComponentCode={targetComponentCode}
              studentFeedback={studentFeedback}
              exclusionReason={exclusionReason}
              requiresExclusionReason={requiresExclusionReason}
              canEditMinute={canEditMinute}
              isLocked={isLocked}
              isPrintPreview={isPrintPreview}
              isPreviewing={isPreviewing}
              onChangeMarking={handleChangeSecretaryMarking}
              onChangeTargetComponent={handleChangeTargetComponent}
              onChangeStudentFeedback={setStudentFeedback}
              onChangeExclusionReason={setExclusionReason}
            />
          </div>

          {!isPrintPreview && (
            <CouncilMinuteAuditDock
              scoreItems={effectiveScoreItems}
              calculationPreview={effectiveCalculation}
              discrepancySummary={effectiveDiscrepancy}
              isPreviewActive={isDirty && (changesExclusion || secretaryMarking !== 'None')}
              isPreviewing={isPreviewing}
              previewError={previewError}
              isDirty={isDirty}
              isLocked={isLocked}
              isCouncilActive={isCouncilActive}
              canEditMinute={canEditMinute}
              canLockMinute={canLockMinute}
              canUnlockMinute={canUnlockMinute}
              isSaving={isSaving}
              isPrintPreview={isPrintPreview}
              onSave={handleSaveMinute}
              onOpenLockModal={() => setIsLockModalOpen(true)}
              onOpenUnlockModal={() => setIsUnlockModalOpen(true)}
              onTogglePrintPreview={() => setIsPrintPreview(true)}
              onPrint={handleTriggerPrint}
            />
          )}
        </div>
      ) : null}

      {/* Drawers & Modals (hidden when printing) */}
      <div className="print:hidden">
        <CouncilMinuteGroupDrawer
          open={isGroupDrawerOpen}
          onClose={() => setIsGroupDrawerOpen(false)}
          items={filteredQueue}
          selectedAssignmentId={selectedAssignmentId}
          search={queueSearch}
          onSearchChange={setQueueSearch}
          filter={queueFilter}
          onFilterChange={setQueueFilter}
          stats={queueStats}
          onSelect={handleSelectAssignment}
        />

        <LockMinuteDialog
          open={isLockModalOpen}
          onClose={() => setIsLockModalOpen(false)}
          onConfirm={handleConfirmLock}
          isLocking={isLocking}
          errorMessage={lockModalError}
          councilCode={activeOverviewRow?.councilCode ?? 'Hội đồng'}
          groupCode={activeOverviewRow?.groupCode ?? ''}
          finalScore={effectiveCalculation?.finalRoundedScore ?? null}
        />

        <UnlockMinuteDialog
          open={isUnlockModalOpen}
          onClose={() => setIsUnlockModalOpen(false)}
          onConfirm={handleConfirmUnlock}
          isUnlocking={isUnlocking}
          errorMessage={unlockModalError}
          councilCode={activeOverviewRow?.councilCode ?? 'Hội đồng'}
          groupCode={activeOverviewRow?.groupCode ?? ''}
        />

        <ConfirmDialog
          open={pendingSwitchId !== null}
          onClose={cancelPendingSwitch}
          onConfirm={confirmDiscardAndSwitch}
          type="warning"
          title="Có thay đổi chưa lưu trên biên bản"
          description={
            pendingRow
              ? `Bạn đang có chỉnh sửa chưa lưu ở nhóm hiện tại. Nếu chuyển sang nhóm ${pendingRow.groupCode} (${pendingRow.councilCode}), các thay đổi nháp sẽ bị hủy.`
              : 'Bạn đang có chỉnh sửa chưa lưu. Nếu chuyển sang nhóm bảo vệ khác, các thay đổi nháp sẽ bị hủy.'
          }
          confirmText="Hủy thay đổi & chuyển nhóm"
          cancelText="Ở lại tiếp tục sửa"
        />
      </div>
    </div>
  );
}

export default CouncilMinutesPage;
