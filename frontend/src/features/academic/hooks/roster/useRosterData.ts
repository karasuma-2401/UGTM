import { useState, useEffect, useMemo, useCallback } from 'react';
import type { KltnBatchSummaryDto } from '../../types/period.types';
import type { UserSummary } from '@/features/identity/types/user.types';
import { periodApi, getPeriodErrorMessage } from '../../services/periodApi';
import { userApi } from '@/features/identity/services/userApi';

export function useRosterData(onError: (msg: string) => void) {
  const [batches, setBatches] = useState<KltnBatchSummaryDto[]>([]);
  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(true);
  const [existingUsers, setExistingUsers] = useState<UserSummary[]>([]);

  useEffect(() => {
    let isMounted = true;
    const fetchInitialData = async () => {
      setIsLoadingBatches(true);
      try {
        const [batchesRes, usersRes] = await Promise.all([
          periodApi.getBatches({ pageSize: 50 }),
          userApi.getUsers({ keyword: '', role: '', status: '', pageNumber: 1, pageSize: 200 }),
        ]);

        if (isMounted) {
          setBatches(batchesRes.items);
          if (batchesRes.items.length > 0) {
            setSelectedBatchId(batchesRes.items[0].id);
          }
          setExistingUsers(usersRes.items);
        }
      } catch (err) {
        if (isMounted) {
          onError(getPeriodErrorMessage(err));
        }
      } finally {
        if (isMounted) {
          setIsLoadingBatches(false);
        }
      }
    };

    fetchInitialData();
    return () => {
      isMounted = false;
    };
  }, [onError]);

  const selectedBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || null,
    [batches, selectedBatchId],
  );

  const refreshUsers = useCallback(async () => {
    try {
      const res = await userApi.getUsers({ keyword: '', role: '', status: '', pageNumber: 1, pageSize: 200 });
      setExistingUsers(res.items);
    } catch {}
  }, []);

  return {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    selectedBatch,
    isLoadingBatches,
    existingUsers,
    refreshUsers,
  };
}
