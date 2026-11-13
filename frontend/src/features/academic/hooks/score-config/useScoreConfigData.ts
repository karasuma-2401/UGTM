import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import type {
  KltnBatchSummaryDto,
  ScoreConfigurationDto,
} from '../../types/scoreConfig.types';
import { scoreConfigApi, getScoreConfigErrorMessage } from '../../services/scoreConfigApi';

export function useScoreConfigData(onError: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchIdState] = useState<string>('');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentConfig, setCurrentConfig] = useState<ScoreConfigurationDto | null>(null);
  const [historyConfigs, setHistoryConfigs] = useState<ScoreConfigurationDto[]>([]);
  const [isLoadingConfig, setIsLoadingConfig] = useState<boolean>(false);

  const onErrorRef = useRef(onError);
  const configRequestIdRef = useRef(0);
  const selectedBatchIdRef = useRef(selectedBatchId);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  useEffect(() => {
    selectedBatchIdRef.current = selectedBatchId;
  }, [selectedBatchId]);

  const setSelectedBatchId = useCallback((nextId: string) => {
    configRequestIdRef.current += 1;
    selectedBatchIdRef.current = nextId;
    setSelectedBatchIdState(nextId);
    setCurrentConfig((prev) => (prev?.batchId === nextId ? prev : null));
    setHistoryConfigs((prev) => (prev.length > 0 && prev[0]?.batchId === nextId ? prev : []));
    setIsLoadingConfig(Boolean(nextId));
  }, []);

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
          onErrorRef.current(getScoreConfigErrorMessage(err));
        }
      } finally {
        if (isMounted) {
          setIsLoadingBatches(false);
        }
      }
    };

    void fetchBatches();
    return () => {
      isMounted = false;
      configRequestIdRef.current += 1;
    };
  }, [setSelectedBatchId]);

  const loadBatchConfig = useCallback(async (batchId: string) => {
    if (!batchId) return null;
    const requestId = ++configRequestIdRef.current;

    setCurrentConfig((prev) => (prev?.batchId === batchId ? prev : null));
    setHistoryConfigs((prev) => (prev.length > 0 && prev[0]?.batchId === batchId ? prev : []));
    setIsLoadingConfig(true);

    try {
      const history = await scoreConfigApi.getConfigurationHistory(batchId);
      if (requestId !== configRequestIdRef.current || selectedBatchIdRef.current !== batchId) {
        return null;
      }

      setHistoryConfigs(history);

      const draftConfig = history.find((c) => c.status === 'Draft');
      let targetConfig = draftConfig || null;

      if (!targetConfig) {
        targetConfig = await scoreConfigApi.getActiveConfiguration(batchId);
        if (requestId !== configRequestIdRef.current || selectedBatchIdRef.current !== batchId) {
          return null;
        }
      }

      setCurrentConfig(targetConfig);
      return targetConfig;
    } catch (err) {
      if (requestId === configRequestIdRef.current && selectedBatchIdRef.current === batchId) {
        onErrorRef.current(getScoreConfigErrorMessage(err));
      }
      return null;
    } finally {
      if (requestId === configRequestIdRef.current) {
        setIsLoadingConfig(false);
      }
    }
  }, []);

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