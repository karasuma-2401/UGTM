import { useEffect, useMemo } from 'react';
import type { ScoreComponentInput, ScoreConfigurationDto } from '../../types/scoreConfig.types';
import {
  isConfigDifferentFromDefault,
  isConfigDifferentFromSaved,
} from '../../utils/scoreWeights';
import { useScoreConfigModals } from './useScoreConfigModals';
import { useScoreConfigData } from './useScoreConfigData';
import { useScoreConfigForm } from './useScoreConfigForm';
import { useScoreConfigActions } from './useScoreConfigActions';

export function useScoreConfig() {
  const modals = useScoreConfigModals();
  const data = useScoreConfigData(modals.setErrorMessage);
  const form = useScoreConfigForm(
    modals.setNotice,
    modals.setErrorMessage,
    () => {
      modals.setEditingComponentTarget(null);
      modals.setIsAddModalOpen(true);
    },
  );
  const actions = useScoreConfigActions();

  const { selectedBatchId, loadBatchConfig } = data;
  const { populateFromConfig } = form;

  const isConfigLocked = Boolean(data.currentConfig && data.currentConfig.status !== 'Draft');

  useEffect(() => {
    let cancelled = false;
    if (selectedBatchId) {
      void loadBatchConfig(selectedBatchId).then((loaded) => {
        if (!cancelled) {
          populateFromConfig(loaded);
        }
      });
    }
    return () => {
      cancelled = true;
    };
  }, [selectedBatchId, loadBatchConfig, populateFromConfig]);

  const isDirty = useMemo(
    () =>
      !isConfigLocked &&
      isConfigDifferentFromSaved(
        data.currentConfig,
        form.calculationMethod,
        form.components,
      ),
    [isConfigLocked, data.currentConfig, form.calculationMethod, form.components],
  );

  const handleApplyDefaultTemplateClick = () => {
    if (isConfigLocked) return;

    const isDifferent = isConfigDifferentFromDefault(
      form.calculationMethod,
      form.reviewerMinimumScore,
      form.components,
    );

    if (isDifferent) {
      modals.setIsResetConfirmModalOpen(true);
      return;
    }

    form.applyDefaultTemplate();
  };

  const applyDefaultTemplateWithModalClose = () => {
    if (isConfigLocked) {
      modals.setIsResetConfirmModalOpen(false);
      return;
    }
    form.applyDefaultTemplate();
    modals.setIsResetConfirmModalOpen(false);
  };

  const handleOpenAddComponent = () => {
    if (isConfigLocked) return;
    modals.setEditingComponentTarget(null);
    modals.setIsAddModalOpen(true);
  };

  const handleOpenEditComponent = (component: ScoreComponentInput, index: number) => {
    if (isConfigLocked) return;
    modals.setEditingComponentTarget({ component, index });
    modals.setIsAddModalOpen(true);
  };

  const handleCloseComponentDialog = () => {
    modals.setIsAddModalOpen(false);
    modals.setEditingComponentTarget(null);
  };

  const handleSaveComponentFromDialog = (savedComponent: ScoreComponentInput) => {
    if (isConfigLocked) return;

    if (modals.editingComponentTarget) {
      form.updateComponent(modals.editingComponentTarget.index, savedComponent);
      modals.setNotice(`Đã cập nhật đầu điểm "${savedComponent.componentName}".`);
    } else {
      form.addComponent(savedComponent);
      modals.setNotice(`Đã thêm đầu điểm "${savedComponent.componentName}".`);
    }
    modals.setEditingComponentTarget(null);
  };

  const handleRequestDeleteComponent = (component: ScoreComponentInput, index: number) => {
    if (isConfigLocked) return;

    modals.setErrorMessage(null);

    if (data.currentConfig?.status === 'Draft') {
      if (form.components.length <= 1) {
        modals.setErrorMessage('Bản nháp cấu hình điểm phải có ít nhất 1 đầu điểm.');
        return;
      }
      modals.setDeletingComponentTarget({ component, index });
      return;
    }

    // Chưa có cấu hình lưu trong DB (đang tạo bảng điểm mới): xóa ngay trên giao diện
    form.removeComponent(index);
    modals.setNotice(`Đã xóa đầu điểm "${component.componentName}".`);
  };

  const handleConfirmDeleteComponent = async () => {
    const target = modals.deletingComponentTarget;
    if (!target || isConfigLocked || data.currentConfig?.status !== 'Draft') {
      modals.setDeletingComponentTarget(null);
      return;
    }

    modals.setErrorMessage(null);
    modals.setNotice(null);

    const nextComponents = form.components
      .filter((_, idx) => idx !== target.index)
      .map((item, idx) => ({
        ...item,
        orderIndex: idx + 1,
      }));

    await actions.deleteComponentFromDraft({
      selectedBatchId: data.selectedBatchId,
      currentConfig: data.currentConfig,
      isLoadingConfig: data.isLoadingConfig,
      calculationMethod: form.calculationMethod,
      nextComponents,
      onSuccess: async () => {
        modals.setDeletingComponentTarget(null);
        modals.setNotice(
          `Đã xóa đầu điểm "${target.component.componentName}" và cập nhật bản nháp cấu hình điểm.`,
        );
        const reloaded = await data.loadBatchConfig(data.selectedBatchId);
        form.populateFromConfig(reloaded);
      },
      onError: (msg) => {
        modals.setDeletingComponentTarget(null);
        modals.setErrorMessage(msg);
      },
    });
  };

  const executeSaveDraft = async (changeReasonText?: string | null) => {
    if (isConfigLocked) return;

    modals.setErrorMessage(null);
    modals.setNotice(null);

    await actions.executeSaveDraft({
      selectedBatchId: data.selectedBatchId,
      currentConfig: data.currentConfig,
      currentBatch: data.currentBatch,
      isLoadingConfig: data.isLoadingConfig,
      calculationMethod: form.calculationMethod,
      components: form.components,
      isValid: form.isValid,
      totalWeight: form.totalWeight,
      changeReasonText,
      onSuccess: async (savedConfig) => {
        const batchTitle = data.currentBatch?.batchName || 'đợt khóa luận';
        modals.setNotice(
          savedConfig.status === 'Draft'
            ? `Đã lưu thành công bản nháp cấu hình điểm (Phiên bản v${savedConfig.versionNo}) cho ${batchTitle}. Bạn có thể tiếp tục chỉnh sửa hoặc bấm "Công bố" để áp dụng chính thức.`
            : `Đã tạo thành công bản nháp cấu hình điểm (Phiên bản v${savedConfig.versionNo}) cho ${batchTitle}.`,
        );
        const reloaded = await data.loadBatchConfig(data.selectedBatchId);
        form.populateFromConfig(reloaded);
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSaveClick = () => {
    if (isConfigLocked) return;
    void executeSaveDraft(null);
  };

  const handlePublish = async () => {
    if (isConfigLocked) return;

    modals.setErrorMessage(null);
    modals.setNotice(null);

    await actions.handlePublish({
      selectedBatchId: data.selectedBatchId,
      currentConfig: data.currentConfig,
      isLoadingConfig: data.isLoadingConfig,
      calculationMethod: form.calculationMethod,
      components: form.components,
      totalWeight: form.totalWeight,
      isDirty,
      isValid: form.isValid,
      onSuccess: async (published) => {
        const batchTitle = data.currentBatch?.batchName || 'đợt khóa luận';
        modals.setNotice(
          `Đã công bố thành công cấu hình điểm phiên bản v${published.versionNo} cho ${batchTitle}. Cấu hình hiện đang có hiệu lực áp dụng!`,
        );
        const reloaded = await data.loadBatchConfig(data.selectedBatchId);
        form.populateFromConfig(reloaded);
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSelectHistoryVersion = (selected: ScoreConfigurationDto) => {
    if (isConfigLocked) return;
    form.populateFromConfig(selected);
    modals.setNotice(
      `Đã nạp thông số từ phiên bản v${selected.versionNo} (${selected.status}) vào biểu mẫu.`,
    );
  };

  return {
    data: {
      ...data,
      isConfigLocked,
    },
    form: {
      ...form,
      isDirty,
    },
    modals,
    actions: {
      isSaving: actions.isSaving,
      isDeletingComponent: actions.isDeletingComponent,
      isPublishing: actions.isPublishing,
      handleApplyDefaultTemplateClick,
      applyDefaultTemplate: applyDefaultTemplateWithModalClose,
      handleOpenAddComponent,
      handleOpenEditComponent,
      handleCloseComponentDialog,
      handleSaveComponentFromDialog,
      handleRequestDeleteComponent,
      handleConfirmDeleteComponent,
      executeSaveDraft,
      handleSaveClick,
      handlePublish,
      handleSelectHistoryVersion,
    },
  };
}
