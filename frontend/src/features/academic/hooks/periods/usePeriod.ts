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

  const { selectedBatchId, setSelectedBatchId: setRawSelectedBatchId, loadBatchDetail, fetchBatches } = data;
  const { resetForm, populateFromDetail } = form;
  const { clearFailedGateUpdates } = actions;
  const { setErrorMessage } = modals;

  const setSelectedBatchId = useCallback(
    (nextId: string) => {
      setErrorMessage(null);
      clearFailedGateUpdates();
      setRawSelectedBatchId(nextId);
    },
    [setErrorMessage, clearFailedGateUpdates, setRawSelectedBatchId],
  );

  useEffect(() => {
    let cancelled = false;
    clearFailedGateUpdates();

    if (selectedBatchId === 'new') {
      resetForm();
    } else if (selectedBatchId) {
      void loadBatchDetail(selectedBatchId).then((result) => {
        if (!cancelled && result?.detail && result.detail.id === selectedBatchId) {
          populateFromDetail(result.detail);
        }
      });
    }

    return () => {
      cancelled = true;
    };
  }, [selectedBatchId, loadBatchDetail, resetForm, populateFromDetail, clearFailedGateUpdates]);

  const reloadCurrentBatch = useCallback(async () => {
    if (selectedBatchId !== 'new') {
      setErrorMessage(null);
      const result = await loadBatchDetail(selectedBatchId);
      if (result?.detail && result.detail.id === selectedBatchId) {
        populateFromDetail(result.detail);
      }
    }
  }, [selectedBatchId, setErrorMessage, loadBatchDetail, populateFromDetail]);

  const executeSave = async (reason?: string) => {
    modals.setNotice(null);
    modals.setErrorMessage(null);

    await actions.executeSave({
      selectedBatchId: data.selectedBatchId,
      currentBatch: data.currentBatch,
      isLoadingDetail: data.isLoadingDetail,
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
      onBatchCreated: async (createdBatchId) => {
        await fetchBatches(createdBatchId);
      },
      onSuccess: async (targetBatchId, message, isNew) => {
        modals.setNotice(message);
        await fetchBatches(targetBatchId);
        if (!isNew) {
          const result = await loadBatchDetail(targetBatchId);
          if (result?.detail && result.detail.id === targetBatchId) {
            populateFromDetail(result.detail);
          }
        }
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleRetryFailedGates = async () => {
    modals.setNotice(null);
    modals.setErrorMessage(null);

    await actions.retryFailedGates({
      selectedBatchId: data.selectedBatchId,
      onSuccess: async (targetBatchId, message) => {
        modals.setNotice(message);
        const result = await loadBatchDetail(targetBatchId);
        if (result?.detail && result.detail.id === targetBatchId) {
          populateFromDetail(result.detail);
        }
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSaveClick = () => {
    if (data.selectedBatchId !== 'new') {
      if (
        data.isLoadingDetail ||
        !data.currentBatch ||
        data.currentBatch.id !== data.selectedBatchId
      ) {
        modals.setErrorMessage(
          'Dữ liệu đợt khóa luận đang chọn chưa tải xong hoặc không khớp với đợt hiện tại. Vui lòng đợi tải hoàn tất rồi thử lại.',
        );
        return;
      }
    }

    if (data.currentBatch && data.currentBatch.status === 'Open') {
      modals.setIsChangeReasonOpen(true);
    } else {
      void executeSave();
    }
  };

  const handleConfirmChangeReason = (reason: string) => {
    modals.setIsChangeReasonOpen(false);
    void executeSave(reason);
  };

  const handleOpenBatch = async () => {
    modals.setNotice(null);
    modals.setErrorMessage(null);
    await actions.handleOpenBatch({
      selectedBatchId: data.selectedBatchId,
      currentBatch: data.currentBatch,
      isLoadingDetail: data.isLoadingDetail,
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
    modals.setNotice(null);
    modals.setErrorMessage(null);
    await actions.handleSaveGates({
      selectedBatchId: data.selectedBatchId,
      currentBatch: data.currentBatch,
      isLoadingDetail: data.isLoadingDetail,
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
    data: {
      ...data,
      setSelectedBatchId,
    },
    form,
    modals,
    actions: {
      isSaving: actions.isSaving,
      isOpenLoading: actions.isOpenLoading,
      failedGateUpdates: actions.failedGateUpdates,
      executeSave,
      handleRetryFailedGates,
      handleSaveClick,
      handleConfirmChangeReason,
      handleOpenBatch,
      handleSaveGates,
      reloadCurrentBatch,
    },
  };
}