import { useState } from 'react';
import type {
  ScoreCalculationMethod,
  ScoreComponentInput,
  ScoreConfigurationDto,
  KltnBatchSummaryDto,
} from '../types/scoreConfig.types';
import { scoreConfigApi, getScoreConfigErrorMessage } from '../services/scoreConfigApi';

interface SaveDraftParams {
  selectedBatchId: string;
  currentConfig: ScoreConfigurationDto | null;
  currentBatch: KltnBatchSummaryDto | null;
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
    isValid,
    onSuccess,
    onError,
  }: PublishParams) => {
    if (!selectedBatchId || !currentConfig || currentConfig.status !== 'Draft') return;
    if (!isValid) {
      onError('Cấu hình chưa hợp lệ. Vui lòng kiểm tra lại tổng trọng số / hệ số.');
      return;
    }

    setIsPublishing(true);
    try {
      const published = await scoreConfigApi.publishConfiguration(
        selectedBatchId,
        currentConfig.id,
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