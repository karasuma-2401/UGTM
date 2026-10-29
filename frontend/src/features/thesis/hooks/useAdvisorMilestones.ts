import { useState, useMemo, useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { advisingApi } from '@/features/topics/services/advisingApi';
import { milestoneApi } from '../services/milestoneApi';
import type {
  AdvisedGroupOption,
  MilestoneWithReportDto,
  CreateProgressMilestoneRequest,
} from '../types/milestone.types';

export function useAdvisorMilestones() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [selectedMilestone, setSelectedMilestone] =
    useState<MilestoneWithReportDto | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const currentBatch = batchesQuery.data?.find(
    (b) => b.status === 'Open'
  ) || batchesQuery.data?.[0];

  const topicsQuery = useQuery({
    queryKey: ['advisor-topics-for-milestones', user?.id, currentBatch?.id],
    queryFn: ({ signal }) =>
      advisingApi.getTopics(user!.id, currentBatch?.id || '', signal),
    enabled: Boolean(user?.id),
    staleTime: 60_000,
  });

  const advisedGroups: AdvisedGroupOption[] = useMemo(() => {
    return (topicsQuery.data || [])
      .filter((t) => Boolean(t.group && t.group.groupId))
      .map((t) => {
        const studentNames =
          t.group!.students?.map((s) => s.fullName).join(' / ') ||
          'Chưa cập nhật thành viên';
        return {
          groupId: t.group!.groupId,
          groupCode: `Nhóm ${t.group!.groupId.slice(0, 4).toUpperCase()}`,
          topicId: t.id,
          topicCode: `KLTN-${t.id.slice(0, 8).toUpperCase()}`,
          topicTitleVi: t.titleVi,
          studentNames,
          students: t.group!.students || [],
        };
      });
  }, [topicsQuery.data]);

  useEffect(() => {
    if (!selectedGroupId && advisedGroups.length > 0) {
      setSelectedGroupId(advisedGroups[0].groupId);
    }
  }, [selectedGroupId, advisedGroups]);

  const selectedGroup = useMemo(() => {
    return advisedGroups.find((g) => g.groupId === selectedGroupId) || null;
  }, [advisedGroups, selectedGroupId]);

  const milestonesQuery = useQuery({
    queryKey: ['advisor-milestones-with-reports', selectedGroupId],
    queryFn: async ({ signal }) => {
      if (!selectedGroupId) return [];
      const milestones = await milestoneApi.getMilestones(selectedGroupId, signal);
      const withReports = await Promise.all(
        milestones.map(async (m) => {
          try {
            const report = await milestoneApi.getProgressReport(m.id, signal);
            return { milestone: m, report };
          } catch {
            return { milestone: m, report: null };
          }
        })
      );
      return withReports;
    },
    enabled: Boolean(selectedGroupId),
  });

  const createMutation = useMutation({
    mutationFn: async (data: CreateProgressMilestoneRequest) => {
      setActionError(null);
      setActionSuccess(null);
      if (!selectedGroupId) {
        throw new Error('Vui lòng chọn nhóm khóa luận hướng dẫn.');
      }
      return await milestoneApi.createMilestone(selectedGroupId, data);
    },
    onSuccess: (data) => {
      setSelectedMilestone(null);
      setActionSuccess(`Thiết lập mốc "${data.title}" thành công!`);
      void queryClient.invalidateQueries({
        queryKey: ['advisor-milestones-with-reports', selectedGroupId],
      });
    },
    onError: (err: Error) => {
      setActionError(err.message || 'Thiết lập mốc thất bại. Vui lòng thử lại.');
    },
  });

  const milestones = milestonesQuery.data || [];

  return {
    batchesQuery,
    currentBatch,
    topicsQuery,
    advisedGroups,
    selectedGroupId,
    setSelectedGroupId,
    selectedGroup,
    milestonesQuery,
    milestones,
    selectedMilestone,
    setSelectedMilestone,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    createMutation,
  };
}
