import { useQuery } from '@tanstack/react-query';
import { academicBatchApi } from '@/shared/academic-periods/academicBatchApi';
import { useAuth } from '@/shared/auth';

export function useCouncilDefenseStage(batchId: string) {
  const { user, hasPermission } = useAuth();
  const canRead = hasPermission('academic.batches.read');
  const query = useQuery({
    queryKey: ['academic-batch-timeline', user?.id, batchId],
    queryFn: ({ signal }) => academicBatchApi.getTimeline(batchId, signal),
    enabled: Boolean(batchId && user?.id && canRead),
    staleTime: 60_000,
    refetchInterval: 60_000,
  });

  return { query, canRead };
}
