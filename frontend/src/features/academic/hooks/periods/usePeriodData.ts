import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import type {
  KltnBatchSummaryDto,
  KltnBatchDetailDto,
  ReviewGateConfigurationDto,
} from '../../types/period.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';

export function usePeriodData(onError: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchIdState] = useState<string>('new');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);

  const [currentBatch, setCurrentBatch] = useState<KltnBatchDetailDto | null>(null);
  const [reviewGates, setReviewGates] = useState<ReviewGateConfigurationDto[]>([]);
  const [isLoadingDetail, setIsLoadingDetail] = useState<boolean>(false);

  const onErrorRef = useRef(onError);
  const batchesRequestIdRef = useRef(0);
  const detailRequestIdRef = useRef(0);
  const selectedBatchIdRef = useRef(selectedBatchId);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  useEffect(() => {
    selectedBatchIdRef.current = selectedBatchId;
  }, [selectedBatchId]);

  const setSelectedBatchId = useCallback((nextId: string) => {
    detailRequestIdRef.current += 1;
    selectedBatchIdRef.current = nextId;
    setSelectedBatchIdState(nextId);
    setCurrentBatch((prev) => (nextId !== 'new' && prev?.id === nextId ? prev : null));
    setReviewGates((prev) =>
      nextId !== 'new' && prev.length > 0 && prev[0]?.batchId === nextId ? prev : [],
    );
    setIsLoadingDetail(nextId !== 'new');
  }, []);

  const fetchBatches = useCallback(async (selectId?: string) => {
    const requestId = ++batchesRequestIdRef.current;
    setIsLoadingBatches(true);
    try {
      const result = await periodApi.getBatches({ pageSize: 50 });
      if (requestId !== batchesRequestIdRef.current) return [];

      setBatches(result.items);

      const nextSelectedId = selectId
        ? selectId
        : result.items.length > 0
          ? result.items[0].id
          : 'new';
      setSelectedBatchId(nextSelectedId);
      return result.items;
    } catch (err) {
      if (requestId === batchesRequestIdRef.current) {
        onErrorRef.current(getPeriodErrorMessage(err));
      }
      return [];
    } finally {
      if (requestId === batchesRequestIdRef.current) {
        setIsLoadingBatches(false);
      }
    }
  }, [setSelectedBatchId]);

  useEffect(() => {
    void fetchBatches();
    return () => {
      batchesRequestIdRef.current += 1;
      detailRequestIdRef.current += 1;
    };
  }, [fetchBatches]);

  const loadBatchDetail = useCallback(async (batchId: string) => {
    const requestId = ++detailRequestIdRef.current;

    if (batchId === 'new') {
      setIsLoadingDetail(false);
      setCurrentBatch(null);
      setReviewGates([]);
      return null;
    }

    setCurrentBatch((prev) => (prev?.id === batchId ? prev : null));
    setReviewGates((prev) => (prev.length > 0 && prev[0]?.batchId === batchId ? prev : []));
    setIsLoadingDetail(true);

    try {
      const [detail, gates] = await Promise.all([
        periodApi.getBatchById(batchId),
        periodApi.getReviewGateConfigurations(batchId),
      ]);

      if (requestId !== detailRequestIdRef.current || selectedBatchIdRef.current !== batchId) {
        return null;
      }

      setCurrentBatch(detail);
      setReviewGates(gates);
      return { detail, gates };
    } catch (err) {
      if (requestId === detailRequestIdRef.current && selectedBatchIdRef.current === batchId) {
        onErrorRef.current(getPeriodErrorMessage(err));
      }
      return null;
    } finally {
      if (requestId === detailRequestIdRef.current) {
        setIsLoadingDetail(false);
      }
    }
  }, []);

  const isSelectionSynced = useMemo(() => {
    if (selectedBatchId === 'new') {
      return !isLoadingDetail && currentBatch === null;
    }
    return Boolean(!isLoadingDetail && currentBatch && currentBatch.id === selectedBatchId);
  }, [selectedBatchId, isLoadingDetail, currentBatch]);

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
    isSelectionSynced,
    syllabusGate,
    midtermGate,
    fetchBatches,
    loadBatchDetail,
  };
}