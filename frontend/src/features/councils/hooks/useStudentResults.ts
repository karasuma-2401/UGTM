import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { finalReportApi } from '@/features/thesis/services/finalReportApi';
import { studentResultsApi } from '../services/studentResultsApi';
import { syllabusApi } from '@/features/thesis/services/syllabusApi';

const STALE_TIME_MS = 60_000;

export function useStudentResults() {
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const statusQuery = useQuery({
    queryKey: ['student-registration-status', user?.id],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
    enabled: Boolean(user?.id),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const groupId = statusQuery.data?.groupId ?? null;
  const isConfirmedMember =
    Boolean(statusQuery.data?.hasRegistered) &&
    Boolean(groupId) &&
    statusQuery.data?.confirmationStatus === 'Confirmed';

  const currentBatch = useMemo(
    () =>
      batchesQuery.data?.find(
        (batch) => batch.id === statusQuery.data?.batchId || batch.status === 'Open',
      ),
    [batchesQuery.data, statusQuery.data?.batchId],
  );

  const groupDetailQuery = useQuery({
    queryKey: ['student-group-detail', user?.id, groupId, currentBatch?.id],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(
        groupId!,
        {
          topicId: statusQuery.data?.topicId ?? undefined,
          topicTitle: statusQuery.data?.topicTitleVi ?? undefined,
          currentUser: user,
          batchName: currentBatch?.batchName,
        },
        signal,
      ),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const scoreQuery = useQuery({
    queryKey: ['student-score-summary', user?.id, groupId],
    queryFn: ({ signal }) => studentResultsApi.getMyScoreSummary(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
    retry: false,
  });

  const finalSubmissionQuery = useQuery({
    queryKey: ['student-final-submission', user?.id, groupId],
    queryFn: ({ signal }) => studentResultsApi.getMyFinalSubmission(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
    retry: false,
  });

  const feedbackQuery = useQuery({
    queryKey: ['student-results-feedback', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getReviewerFeedback(groupId!, signal),
    enabled: Boolean(user?.id && groupId && isConfirmedMember),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
    retry: false,
  });

  const refresh = async () => {
    if (!isConfirmedMember) {
      await statusQuery.refetch();
      return;
    }
    await Promise.all([
      groupDetailQuery.refetch(),
      scoreQuery.refetch(),
      finalSubmissionQuery.refetch(),
      feedbackQuery.refetch(),
    ]);
  };

  return {
    statusQuery,
    groupDetailQuery,
    scoreQuery,
    finalSubmissionQuery,
    feedbackQuery,
    isConfirmedMember,
    refresh,
  };
}
