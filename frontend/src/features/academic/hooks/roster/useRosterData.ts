import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import type { KltnBatchSummaryDto } from '../../types/period.types';
import type { UserSummary } from '@/features/identity/types/user.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';
import { userApi, getUserErrorMessage } from '@/features/identity/services/userApi';

export function useRosterData(onError?: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);
  const [existingUsers, setExistingUsers] = useState<UserSummary[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  const onErrorRef = useRef(onError);
  const requestIdRef = useRef(0);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  const reloadData = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setIsLoadingBatches(true);
    setLoadError(null);
    try {
      const [batchesRes, usersRes] = await Promise.all([
        periodApi.getBatches({ pageSize: 50 }),
        userApi.getUsers({ keyword: '', role: '', status: '', pageNumber: 1, pageSize: 200 }),
      ]);

      if (requestId !== requestIdRef.current) return;

      setBatches(batchesRes.items);
      setSelectedBatchId((prev) => {
        if (prev && batchesRes.items.some((b) => b.id === prev)) {
          return prev;
        }
        return batchesRes.items.length > 0 ? batchesRes.items[0].id : '';
      });
      setExistingUsers(usersRes.items);
    } catch (err) {
      if (requestId !== requestIdRef.current) return;
      const message = getPeriodErrorMessage(err);
      setLoadError(message);
      onErrorRef.current?.(message);
    } finally {
      if (requestId === requestIdRef.current) {
        setIsLoadingBatches(false);
      }
    }
  }, []);

  useEffect(() => {
    void reloadData();
    return () => {
      requestIdRef.current += 1;
    };
  }, [reloadData]);

  const selectedBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || null,
    [batches, selectedBatchId],
  );

  const refreshUsers = useCallback(async () => {
    try {
      const res = await userApi.getUsers({ keyword: '', role: '', status: '', pageNumber: 1, pageSize: 200 });
      setExistingUsers(res.items);
      setLoadError(null);
    } catch (err) {
      const message = getUserErrorMessage(err);
      setLoadError(message);
      onErrorRef.current?.(message);
    }
  }, []);

  return {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    selectedBatch,
    isLoadingBatches,
    existingUsers,
    loadError,
    setLoadError,
    reloadData,
    refreshUsers,
  };
}
