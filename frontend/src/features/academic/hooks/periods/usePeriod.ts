import { useEffect, useCallback } from 'react';
import { useAuth } from '@/shared/auth';
import { usePeriodModals } from './usePeriodModals';
import { usePeriodData } from './usePeriodData';
import { usePeriodSchedule } from './usePeriodSchedule';
import { usePeriodActions } from './usePeriodActions';

export function usePeriod() {
  const { hasPermission } = useAuth();
  const canManageBatch = hasPermission('academic.batches.manage');
  const canManageReviewGate = hasPermission('thesis.review-gate-config.manage');
  const canEdit = canManageBatch || canManageReviewGate;

  const permissions = {
    canManageBatch,
    canManageReviewGate,
    canEdit,
  };

  const modals = usePeriodModals();
  const data = usePeriodData(modals.setErrorMessage);
  const form = usePeriodSchedule();
  const actions = usePeriodActions();

  const { selectedBatchId, loadBatchDetail, fetchBatches } = data;
  const { resetForm, populateFromDetail } = form;

  useEffect(() => {
    if (selectedBatchId === 'new') {
      resetForm();
    } else if (selectedBatchId) {
      loadBatchDetail(selectedBatchId).then((result) => {
        if (result?.detail) {
          populateFromDetail(result.detail);
        }
      });
    }
  }, [selectedBatchId, loadBatchDetail, resetForm, populateFromDetail]);

  const reloadCurrentBatch = useCallback(async () => {
    if (selectedBatchId !== 'new') {
      const result = await loadBatchDetail(selectedBatchId);
      if (result?.detail) {
        populateFromDetail(result.detail);
      }
    }
  }, [selectedBatchId, loadBatchDetail, populateFromDetail]);

  const executeSave = async (reason?: string) => {
    modals.setNotice(null);
    modals.setErrorMessage(null);

    await actions.executeSave({
      selectedBatchId: data.selectedBatchId,
      currentBatch: data.currentBatch,
      startDate: form.startDate,
      batchName: form.batchName,
      academicYear: form.academicYear,
      semester: form.semester,
      computedStages: form.computedStages,
      syllabusDeadline: form.syllabusDeadline,
      midtermDeadline: form.midtermDeadline,
      finalEvaluationDeadline: form.finalEvaluationDeadline,
      defenseDeadline: form.defenseDeadline,
      changeReason: reason,
      onSuccess: async (targetBatchId, message, isNew) => {
        modals.setNotice(message);
        if (isNew) {
          await fetchBatches(targetBatchId);
        } else {
          const result = await loadBatchDetail(targetBatchId);
          if (result?.detail) {
            populateFromDetail(result.detail);
          }
        }
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSaveClick = () => {
    if (data.currentBatch && data.currentBatch.status === 'Open') {
      modals.setIsChangeReasonOpen(true);
    } else {
      executeSave();
    }
  };

  const handleConfirmChangeReason = (reason: string) => {
    modals.setIsChangeReasonOpen(false);
    executeSave(reason);
  };

  const handleOpenBatch = async () => {
    modals.setNotice(null);
    modals.setErrorMessage(null);
    await actions.handleOpenBatch({
      currentBatch: data.currentBatch,
      onSuccess: async (opened) => {
        data.setCurrentBatch(opened);
        modals.setNotice('Đợt KLTN đã được mở vận hành thành công!');
        await fetchBatches(opened.id);
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSaveGates = async (
    syllabus: { initial: string; faculty: string; final: string },
    midterm: { initial: string; faculty: string; final: string },
  ) => {
    await actions.handleSaveGates({
      currentBatch: data.currentBatch,
      syllabus,
      midterm,
      onSyllabusDeadlineChange: form.handleSyllabusDeadlineChange,
      onMidtermDeadlineChange: form.handleMidtermDeadlineChange,
      onSuccess: (updatedGates) => {
        data.setReviewGates(updatedGates);
        modals.setNotice('Cập nhật chi tiết cổng thẩm định thành công!');
      },
      onError: modals.setErrorMessage,
    });
  };

  return {
    permissions,
    data,
    form,
    modals,
    actions: {
      isSaving: actions.isSaving,
      isOpenLoading: actions.isOpenLoading,
      executeSave,
      handleSaveClick,
      handleConfirmChangeReason,
      handleOpenBatch,
      handleSaveGates,
      reloadCurrentBatch,
    },
  };
}