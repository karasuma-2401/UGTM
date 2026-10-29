import { useState, useEffect, useCallback, useMemo } from 'react';
import type {
  KltnBatchSummaryDto,
  KltnBatchDetailDto,
  ReviewGateConfigurationDto,
} from '../../types/period.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';

export function usePeriodData(onError: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('new');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentBatch, setCurrentBatch] = useState<KltnBatchDetailDto | null>(null);
  const [reviewGates, setReviewGates] = useState<ReviewGateConfigurationDto[]>([]);
  const [isLoadingDetail, setIsLoadingDetail] = useState<boolean>(false);

  const fetchBatches = useCallback(
    async (selectId?: string) => {
      setIsLoadingBatches(true);
      try {
        const result = await periodApi.getBatches({ pageSize: 50 });
        setBatches(result.items);

        if (selectId) {
          setSelectedBatchId(selectId);
        } else if (result.items.length > 0) {
          setSelectedBatchId(result.items[0].id);
        } else {
          setSelectedBatchId('new');
        }
        return result.items;
      } catch (err) {
        onError(getPeriodErrorMessage(err));
        return [];
      } finally {
        setIsLoadingBatches(false);
      }
    },
    [onError],
  );

  useEffect(() => {
    fetchBatches();
  }, [fetchBatches]);

  const loadBatchDetail = useCallback(
    async (batchId: string) => {
      if (batchId === 'new') {
        setCurrentBatch(null);
        setReviewGates([]);
        return null;
      }

      setIsLoadingDetail(true);
      try {
        const [detail, gates] = await Promise.all([
          periodApi.getBatchById(batchId),
          periodApi.getReviewGateConfigurations(batchId),
        ]);
        setCurrentBatch(detail);
        setReviewGates(gates);
        return { detail, gates };
      } catch (err) {
        onError(getPeriodErrorMessage(err));
        return null;
      } finally {
        setIsLoadingDetail(false);
      }
    },
    [onError],
  );

  const syllabusGate = useMemo(
    () => reviewGates.find((g) => g.gateType === 'Syllabus') || null,
    [reviewGates],
  );

  const midtermGate = useMemo(
    () => reviewGates.find((g) => g.gateType === 'Midterm') || null,
    [reviewGates],
  );

  return {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    isLoadingBatches,
    currentBatch,
    setCurrentBatch,
    reviewGates,
    setReviewGates,
    isLoadingDetail,
    syllabusGate,
    midtermGate,
    fetchBatches,
    loadBatchDetail,
  };
}