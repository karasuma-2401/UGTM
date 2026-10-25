import { useState, useEffect, useCallback, useMemo } from 'react';
import type {
  KltnBatchSummaryDto,
  ScoreConfigurationDto,
} from '../types/scoreConfig.types';
import { scoreConfigApi, getScoreConfigErrorMessage } from '../services/scoreConfigApi';

export function useScoreConfigData(onError: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentConfig, setCurrentConfig] = useState<ScoreConfigurationDto | null>(null);
  const [historyConfigs, setHistoryConfigs] = useState<ScoreConfigurationDto[]>([]);
  const [isLoadingConfig, setIsLoadingConfig] = useState<boolean>(false);

  useEffect(() => {
    let isMounted = true;
    const fetchBatches = async () => {
      setIsLoadingBatches(true);
      try {
        const result = await scoreConfigApi.getBatches({ pageSize: 50 });
        if (isMounted) {
          setBatches(result.items);
          if (result.items.length > 0) {
            setSelectedBatchId(result.items[0].id);
          }
        }
      } catch (err) {
        if (isMounted) {
          onError(getScoreConfigErrorMessage(err));
        }
      } finally {
        if (isMounted) {
          setIsLoadingBatches(false);
        }
      }
    };

    fetchBatches();
    return () => {
      isMounted = false;
    };
  }, [onError]);

  const loadBatchConfig = useCallback(async (batchId: string) => {
    if (!batchId) return null;
    setIsLoadingConfig(true);
    try {
      const history = await scoreConfigApi.getConfigurationHistory(batchId);
      setHistoryConfigs(history);

      const draftConfig = history.find((c) => c.status === 'Draft');
      let targetConfig = draftConfig || null;

      if (!targetConfig) {
        targetConfig = await scoreConfigApi.getActiveConfiguration(batchId);
      }

      setCurrentConfig(targetConfig);
      return targetConfig;
    } catch (err) {
      onError(getScoreConfigErrorMessage(err));
      return null;
    } finally {
      setIsLoadingConfig(false);
    }
  }, [onError]);

  const currentBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || null,
    [batches, selectedBatchId],
  );

  return {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    isLoadingBatches,
    currentConfig,
    setCurrentConfig,
    historyConfigs,
    setHistoryConfigs,
    isLoadingConfig,
    currentBatch,
    loadBatchConfig,
  };
}