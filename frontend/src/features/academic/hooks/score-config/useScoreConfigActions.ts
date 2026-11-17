import { useState } from 'react';
import type {
  ScoreCalculationMethod,
  ScoreComponentInput,
  ScoreConfigurationDto,
  KltnBatchSummaryDto,
} from '../../types/scoreConfig.types';
import { scoreConfigApi, getScoreConfigErrorMessage } from '../../services/scoreConfigApi';
import { isConfigDifferentFromSaved } from '../../utils/scoreWeights';

interface SaveDraftParams {
  selectedBatchId: string;
  currentConfig: ScoreConfigurationDto | null;
  currentBatch: KltnBatchSummaryDto | null;
  isLoadingConfig?: boolean;
  calculationMethod: ScoreCalculationMethod;
  components: ScoreComponentInput[];
  isValid: boolean;
  totalWeight: number;
  changeReasonText?: string | null;
  onSuccess: (savedConfig: ScoreConfigurationDto) => void;
  onError: (msg: string) => void;
}

interface PublishParams {
  selectedBatchId: string;
  currentConfig: ScoreConfigurationDto | null;
  isLoadingConfig?: boolean;
  calculationMethod?: ScoreCalculationMethod;
  components?: ScoreComponentInput[];
  totalWeight?: number;
  changeReasonText?: string | null;
  isDirty?: boolean;
  isValid: boolean;
  onSuccess: (published: ScoreConfigurationDto) => void;
  onError: (msg: string) => void;
}

export function useScoreConfigActions() {
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isPublishing, setIsPublishing] = useState<boolean>(false);

  const executeSaveDraft = async ({
    selectedBatchId,
    currentConfig,
    isLoadingConfig = false,
    calculationMethod,
    components,
    isValid,
    totalWeight,
    changeReasonText,
    onSuccess,
    onError,
  }: SaveDraftParams) => {
    if (!selectedBatchId) {
      onError('Vui lòng chọn đợt khóa luận áp dụng trước khi lưu.');
      return;
    }

    if (
      isLoadingConfig ||
      (currentConfig?.batchId && currentConfig.batchId !== selectedBatchId)
    ) {
      onError(
        'Dữ liệu cấu hình của đợt khóa luận đang chọn chưa tải xong hoặc không khớp. Vui lòng đợi tải hoàn tất rồi thử lại.',
      );
      return;
    }

    if (!isValid) {
      if (calculationMethod === 'Percentage') {
        onError(
          `Cấu hình chưa hợp lệ. Tổng tỷ lệ phần trăm hiện tại là ${totalWeight}%, cần đạt chính xác 100%.`,
        );
      } else {
        onError('Cấu hình chưa hợp lệ. Tổng các hệ số phải lớn hơn 0.');
      }
      return;
    }

    setIsSaving(true);
    try {
      let savedConfig: ScoreConfigurationDto;
      if (currentConfig && currentConfig.status === 'Draft') {
        savedConfig = await scoreConfigApi.updateDraft(selectedBatchId, currentConfig.id, {
          calculationMethod,
          components,
          changeReason: changeReasonText || currentConfig.changeReason || null,
        });
      } else {
        savedConfig = await scoreConfigApi.createDraft(selectedBatchId, {
          calculationMethod,
          components,
          changeReason: changeReasonText || null,
        });
      }

      onSuccess(savedConfig);
    } catch (err) {
      onError(getScoreConfigErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const handlePublish = async ({
    selectedBatchId,
    currentConfig,
    isLoadingConfig = false,
    calculationMethod,
    components,
    totalWeight,
    changeReasonText,
    isDirty,
    isValid,
    onSuccess,
    onError,
  }: PublishParams) => {
    if (!selectedBatchId || !currentConfig || currentConfig.status !== 'Draft') return;

    if (
      isLoadingConfig ||
      (currentConfig.batchId && currentConfig.batchId !== selectedBatchId)
    ) {
      onError(
        'Dữ liệu cấu hình của đợt khóa luận đang chọn chưa tải xong hoặc không khớp. Vui lòng đợi tải hoàn tất rồi thử lại.',
      );
      return;
    }

    if (!isValid) {
      if (calculationMethod === 'Percentage' && totalWeight !== undefined) {
        onError(
          `Cấu hình chưa hợp lệ. Tổng tỷ lệ phần trăm hiện tại là ${totalWeight}%, cần đạt chính xác 100%.`,
        );
      } else {
        onError('Cấu hình chưa hợp lệ. Vui lòng kiểm tra lại tổng trọng số / hệ số.');
      }
      return;
    }

    const hasUnsavedChanges =
      isDirty ??
      (calculationMethod !== undefined && components !== undefined
        ? isConfigDifferentFromSaved(currentConfig, calculationMethod, components)
        : false);

    if (hasUnsavedChanges && (calculationMethod === undefined || components === undefined)) {
      onError('Cấu hình có thay đổi chưa lưu. Vui lòng lưu bản nháp trước khi công bố.');
      return;
    }

    setIsPublishing(true);
    try {
      let targetDraftId = currentConfig.id;

      if (hasUnsavedChanges && calculationMethod !== undefined && components !== undefined) {
        const savedDraft = await scoreConfigApi.updateDraft(selectedBatchId, currentConfig.id, {
          calculationMethod,
          components,
          changeReason: changeReasonText || currentConfig.changeReason || null,
        });
        targetDraftId = savedDraft.id;
      }

      const published = await scoreConfigApi.publishConfiguration(
        selectedBatchId,
        targetDraftId,
      );
      onSuccess(published);
    } catch (err) {
      onError(getScoreConfigErrorMessage(err));
    } finally {
      setIsPublishing(false);
    }
  };

  return {
    isSaving,
    isPublishing,
    executeSaveDraft,
    handlePublish,
  };
}