import { useEffect } from 'react';
import type { ScoreConfigurationDto } from '../../types/scoreConfig.types';
import { isConfigDifferentFromDefault } from '../../utils/scoreWeights';
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
    () => modals.setIsAddModalOpen(true),
  );
  const actions = useScoreConfigActions();

  const { selectedBatchId, loadBatchConfig } = data;
  const { populateFromConfig } = form;

  useEffect(() => {
    if (selectedBatchId) {
      loadBatchConfig(selectedBatchId).then((loaded) => {
        populateFromConfig(loaded);
      });
    }
  }, [selectedBatchId, loadBatchConfig, populateFromConfig]);

  const handleApplyDefaultTemplateClick = () => {
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
    form.applyDefaultTemplate();
    modals.setIsResetConfirmModalOpen(false);
  };

  const executeSaveDraft = async (changeReasonText?: string | null) => {
    modals.setErrorMessage(null);
    modals.setNotice(null);

    await actions.executeSaveDraft({
      selectedBatchId: data.selectedBatchId,
      currentConfig: data.currentConfig,
      currentBatch: data.currentBatch,
      calculationMethod: form.calculationMethod,
      components: form.components,
      isValid: form.isValid,
      totalWeight: form.totalWeight,
      changeReasonText,
      onSuccess: async (savedConfig) => {
        const batchTitle = data.currentBatch?.batchName || 'đợt khóa luận';
        modals.setNotice(
          savedConfig.status === 'Draft'
            ? `Đã lưu thành công bản nháp cấu hình điểm (Phiên bản v${savedConfig.versionNo}) cho ${batchTitle}. Bạn có thể tiếp tục chỉnh sửa hoặc bấm "Công bố (Publish)" để áp dụng chính thức.`
            : `Đã tạo thành công bản nháp cấu hình điểm (Phiên bản v${savedConfig.versionNo}) cho ${batchTitle}.`,
        );
        const reloaded = await data.loadBatchConfig(data.selectedBatchId);
        form.populateFromConfig(reloaded);
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSaveClick = () => {
    if (data.currentConfig && data.currentConfig.status === 'Active') {
      modals.setIsCloneReasonModalOpen(true);
      return;
    }

    executeSaveDraft(null);
  };

  const handlePublish = async () => {
    modals.setErrorMessage(null);
    modals.setNotice(null);

    await actions.handlePublish({
      selectedBatchId: data.selectedBatchId,
      currentConfig: data.currentConfig,
      isValid: form.isValid,
      onSuccess: async (published) => {
        const batchTitle = data.currentBatch?.batchName || 'đợt khóa luận';
        modals.setNotice(
          `Đã công bố (Publish) thành công cấu hình điểm phiên bản v${published.versionNo} cho ${batchTitle}. Cấu hình hiện đang có hiệu lực áp dụng!`,
        );
        const reloaded = await data.loadBatchConfig(data.selectedBatchId);
        form.populateFromConfig(reloaded);
      },
      onError: modals.setErrorMessage,
    });
  };

  const handleSelectHistoryVersion = (selected: ScoreConfigurationDto) => {
    form.populateFromConfig(selected);
    modals.setNotice(`Đang xem thông số phiên bản ${selected.versionNo} (${selected.status}).`);
  };

  return {
    data,
    form,
    modals,
    actions: {
      isSaving: actions.isSaving,
      isPublishing: actions.isPublishing,
      handleApplyDefaultTemplateClick,
      applyDefaultTemplate: applyDefaultTemplateWithModalClose,
      executeSaveDraft,
      handleSaveClick,
      handlePublish,
      handleSelectHistoryVersion,
    },
  };
}