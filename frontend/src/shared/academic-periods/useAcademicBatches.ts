import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { academicBatchApi } from './academicBatchApi';

export function useAcademicBatches() {
  const { user, hasPermission } = useAuth();
  return useQuery({
    queryKey: ['academic-batches', user?.id],
    queryFn: ({ signal }) => academicBatchApi.getAll(signal),
    enabled: Boolean(user?.id) && hasPermission('academic.batches.read'),
    staleTime: 60_000,
    refetchInterval: 60_000,
  });
}
