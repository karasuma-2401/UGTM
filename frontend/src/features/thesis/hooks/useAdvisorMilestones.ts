import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { advisingApi } from '@/features/topics/services/advisingApi';
import type { AcademicBatch } from '@/shared/academic-periods/types';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { milestoneApi } from '../services/milestoneApi';
import { syllabusApi } from '../services/syllabusApi';
import type {
  AdvisedGroupOption,
  CreateProgressMilestoneRequest,
  MilestoneWithReportDto,
  ProgressReportStatus,
} from '../types/milestone.types';
import type { SyllabusStatus } from '../types/syllabus.types';
import {
  toReportStatus,
  type ProgressReportReviewRequest,
} from '../utils/milestoneFormatters';
import { getThesisErrorMessage } from '../utils/thesisErrors';

export type MilestoneStatusFilter = 'All' | ProgressReportStatus;

export interface MilestoneStats {
  total: number;
  notSubmitted: number;
  submitted: number;
  accepted: number;
  needRevision: number;
}

const POST_GATE1_GROUP_STATUSES = new Set(['SyllabusApproved', 'InProgress', 'MidtermSubmitted']);
const LOCKED_FINAL_GROUP_STATUSES = new Set([
  'FinalReportSubmitted',
  'ReadyForReviewerAssignment',
  'Defended',
  'ScoreLocked',
  'Completed',
]);

export function resolveDefaultBatchId(batches: readonly AcademicBatch[]): string {
  return batches.find((batch) => batch.status === 'Open')?.id ?? batches[0]?.id ?? '';
}

export function computeMilestoneEligibility(
  selectedGroup: AdvisedGroupOption | null,
  syllabusStatus: SyllabusStatus | null | undefined,
): { canCreateMilestones: boolean; ineligibleReason: string | null } {
  if (!selectedGroup) {
    return { canCreateMilestones: false, ineligibleReason: null };
  }

  const groupStatus = selectedGroup.status ?? '';
  if (groupStatus === 'Stopped') {
    return {
      canCreateMilestones: false,
      ineligibleReason: 'Nhóm đã dừng thực hiện đề tài nên không thể tạo thêm mốc tiến độ.',
    };
  }

  if (LOCKED_FINAL_GROUP_STATUSES.has(groupStatus)) {
    return {
      canCreateMilestones: false,
      ineligibleReason: 'Nhóm đã nộp báo cáo cuối kỳ hoặc hoàn tất giai đoạn thực hiện KLTN.',
    };
  }

  const isSyllabusApproved =
    syllabusStatus === 'Approved' || POST_GATE1_GROUP_STATUSES.has(groupStatus);

  if (!isSyllabusApproved) {
    return {
      canCreateMilestones: false,
      ineligibleReason:
        'Nhóm chưa được duyệt Đề cương chi tiết tại Cổng 1. GVHD có thể tạo mốc tiến độ sau khi Đề cương được phê duyệt.',
    };
  }

  return { canCreateMilestones: true, ineligibleReason: null };
}

export function computeMilestoneStats(items: readonly MilestoneWithReportDto[]): MilestoneStats {
  const stats: MilestoneStats = {
    total: items.length,
    notSubmitted: 0,
    submitted: 0,
    accepted: 0,
    needRevision: 0,
  };

  for (const item of items) {
    const status = toReportStatus(item.report?.currentStatus);
    if (status === 'Submitted') stats.submitted += 1;
    else if (status === 'Accepted') stats.accepted += 1;
    else if (status === 'NeedRevision') stats.needRevision += 1;
    else stats.notSubmitted += 1;
  }

  return stats;
}

export function filterMilestonesByStatus(
  items: readonly MilestoneWithReportDto[],
  statusFilter: MilestoneStatusFilter,
): MilestoneWithReportDto[] {
  if (statusFilter === 'All') return [...items];
  return items.filter(
    (item) => (item.report?.currentStatus ?? 'NotSubmitted') === statusFilter,
  );
}

export function useAdvisorMilestones() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [userBatchId, setUserBatchId] = useState<string | null>(null);
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [selectedMilestoneId, setSelectedMilestoneId] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<MilestoneStatusFilter>('All');
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [reviewingMilestoneId, setReviewingMilestoneId] = useState<string | null>(null);
  const [downloadingFileId, setDownloadingFileId] = useState<string | null>(null);

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const selectedBatchId = useMemo(() => {
    if (userBatchId && batches.some((batch) => batch.id === userBatchId)) {
      return userBatchId;
    }
    return resolveDefaultBatchId(batches);
  }, [batches, userBatchId]);

  const currentBatch = useMemo(
    () => batches.find((batch) => batch.id === selectedBatchId) ?? null,
    [batches, selectedBatchId],
  );

  const advisorUserId = user?.id;

  const topicsQuery = useQuery({
    queryKey: ['advising', 'topics', advisorUserId, selectedBatchId],
    queryFn: ({ signal }) => advisingApi.getTopics(advisorUserId!, selectedBatchId, signal),
    enabled: Boolean(advisorUserId && selectedBatchId),
    staleTime: 30_000,
  });

  const advisedGroups = useMemo<AdvisedGroupOption[]>(() => {
    const topics = topicsQuery.data ?? [];
    const statusPriority = (status?: string | null) => {
      if (status && POST_GATE1_GROUP_STATUSES.has(status)) return 0;
      if (status && LOCKED_FINAL_GROUP_STATUSES.has(status)) return 2;
      if (status === 'Stopped') return 3;
      return 1;
    };

    return topics
      .filter((topic) => {
        const ownsTopic = !advisorUserId || topic.advisorId === advisorUserId;
        return ownsTopic && Boolean(topic.group?.groupId);
      })
      .map((topic) => {
        const group = topic.group!;
        const students = (group.students ?? []).map((student) => ({
          studentId: student.studentId,
          fullName: student.fullName || student.userCode || 'Sinh viên',
          userCode: student.userCode ?? null,
        }));
        const studentNames =
          students.map((student) => student.fullName).join(', ') || 'Chưa có thành viên';

        return {
          groupId: group.groupId,
          groupCode: `NH-${group.groupId.slice(-6).toUpperCase()}`,
          topicId: topic.id,
          topicCode: `DT-${topic.id.slice(-6).toUpperCase()}`,
          topicTitleVi: topic.titleVi,
          studentNames,
          status: group.status,
          students,
        };
      })
      .sort((a, b) => statusPriority(a.status) - statusPriority(b.status));
  }, [topicsQuery.data, advisorUserId]);

  const effectiveGroupId = useMemo(() => {
    if (selectedGroupId && advisedGroups.some((group) => group.groupId === selectedGroupId)) {
      return selectedGroupId;
    }
    return advisedGroups[0]?.groupId ?? null;
  }, [advisedGroups, selectedGroupId]);

  const selectedGroup = useMemo(
    () => advisedGroups.find((group) => group.groupId === effectiveGroupId) ?? null,
    [advisedGroups, effectiveGroupId],
  );

  const syllabusQuery = useQuery({
    queryKey: ['thesis', 'syllabus', effectiveGroupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(effectiveGroupId!, signal),
    enabled: Boolean(effectiveGroupId),
    staleTime: 30_000,
  });

  const milestonesQuery = useQuery({
    queryKey: ['thesis', 'milestones', effectiveGroupId],
    queryFn: async ({ signal }): Promise<MilestoneWithReportDto[]> => {
      const list = await milestoneApi.getMilestones(effectiveGroupId!, signal);
      const sorted = [...list].sort(
        (a, b) => new Date(a.deadlineUtc).getTime() - new Date(b.deadlineUtc).getTime(),
      );
      const reports = await Promise.all(
        sorted.map((milestone) =>
          milestoneApi.getProgressReport(milestone.id, signal).catch(() => null),
        ),
      );
      return sorted.map((milestone, index) => ({
        milestone,
        report: reports[index] ?? null,
      }));
    },
    enabled: Boolean(effectiveGroupId),
    staleTime: 15_000,
  });

  const milestones = useMemo(() => milestonesQuery.data ?? [], [milestonesQuery.data]);
  const stats = useMemo(() => computeMilestoneStats(milestones), [milestones]);
  const filteredMilestones = useMemo(
    () => filterMilestonesByStatus(milestones, statusFilter),
    [milestones, statusFilter],
  );

  const effectiveMilestoneId = useMemo(() => {
    if (
      selectedMilestoneId &&
      milestones.some((item) => item.milestone.id === selectedMilestoneId)
    ) {
      return selectedMilestoneId;
    }
    return filteredMilestones[0]?.milestone.id ?? milestones[0]?.milestone.id ?? null;
  }, [filteredMilestones, milestones, selectedMilestoneId]);

  const selectedMilestone = useMemo(
    () => milestones.find((item) => item.milestone.id === effectiveMilestoneId) ?? null,
    [milestones, effectiveMilestoneId],
  );

  const reviewingMilestone = useMemo(
    () =>
      reviewingMilestoneId
        ? (milestones.find((item) => item.milestone.id === reviewingMilestoneId) ?? null)
        : null,
    [milestones, reviewingMilestoneId],
  );

  const syllabusStatus = syllabusQuery.data?.currentStatus ?? null;
  const { canCreateMilestones, ineligibleReason } = useMemo(
    () => computeMilestoneEligibility(selectedGroup, syllabusStatus),
    [selectedGroup, syllabusStatus],
  );

  const createMutation = useMutation({
    mutationFn: async (request: CreateProgressMilestoneRequest) => {
      if (!effectiveGroupId) {
        throw new Error('Vui lòng chọn nhóm hướng dẫn trước khi tạo mốc tiến độ.');
      }
      return milestoneApi.createMilestone(effectiveGroupId, request);
    },
    onSuccess: (created) => {
      setSelectedMilestoneId(created.id);
      setStatusFilter('All');
      setCreateDialogOpen(false);
      void queryClient.invalidateQueries({ queryKey: ['thesis', 'milestones', effectiveGroupId] });
      notify.success(`Mốc "${created.title}" đã được giao cho nhóm ${selectedGroup?.groupCode ?? ''}.`, {
        title: 'Đã thêm mốc tiến độ',
      });
    },
    onError: (err) => {
      notify.error(getThesisErrorMessage(err, 'Không thể tạo mốc tiến độ.'), {
        title: 'Không thể tạo mốc tiến độ',
      });
    },
  });

  const reviewMutation = useMutation({
    mutationFn: async ({
      milestoneId,
      data,
    }: {
      milestoneId: string;
      data: ProgressReportReviewRequest;
    }) => milestoneApi.reviewProgressReport(milestoneId, data),
    onSuccess: (reviewResult, variables) => {
      setReviewingMilestoneId(null);
      setSelectedMilestoneId(variables.milestoneId);
      void queryClient.invalidateQueries({ queryKey: ['thesis', 'milestones', effectiveGroupId] });
      void queryClient.invalidateQueries({
        queryKey: ['thesis', 'progress-report', variables.milestoneId],
      });
      void queryClient.invalidateQueries({
        queryKey: ['thesis', 'group-detail', effectiveGroupId],
      });
      if (reviewResult.decision === 'Accepted') {
        notify.success('Nhóm sinh viên có thể tiếp tục các mốc tiếp theo.', {
          title: 'Đã xác nhận đạt mốc tiến độ',
        });
      } else {
        notify.warning('Nhóm sinh viên đã được thông báo bổ sung báo cáo.', {
          title: 'Đã gửi yêu cầu chỉnh sửa',
        });
      }
    },
    onError: (err) => {
      notify.error(getThesisErrorMessage(err, 'Không thể gửi đánh giá.'), {
        title: 'Không thể gửi đánh giá',
      });
    },
  });

  async function handleDownloadFile(fileId: string, fileName?: string) {
    try {
      setDownloadingFileId(fileId);
      const { downloadUrl } = await milestoneApi.getDownloadUrl(fileId);
      const anchor = document.createElement('a');
      anchor.href = downloadUrl;
      if (fileName) anchor.download = fileName;
      anchor.target = '_blank';
      anchor.rel = 'noopener noreferrer';
      anchor.click();
    } catch (err) {
      notify.error(getThesisErrorMessage(err, 'Không thể tải tệp báo cáo.'), {
        title: 'Không thể tải tệp báo cáo',
      });
    } finally {
      setDownloadingFileId(null);
    }
  }

  function handleBatchChange(batchId: string) {
    setUserBatchId(batchId);
    setSelectedGroupId(null);
    setSelectedMilestoneId(null);
    setReviewingMilestoneId(null);
    setStatusFilter('All');
  }

  function handleGroupChange(groupId: string) {
    setSelectedGroupId(groupId);
    setSelectedMilestoneId(null);
    setReviewingMilestoneId(null);
    setStatusFilter('All');
  }

  function handleSelectMilestone(item: MilestoneWithReportDto | null) {
    setSelectedMilestoneId(item?.milestone.id ?? null);
  }

  function openCreateDialog() {
    createMutation.reset();
    setCreateDialogOpen(true);
  }

  function closeCreateDialog() {
    if (createMutation.isPending) return;
    setCreateDialogOpen(false);
  }

  function openReviewDialog(milestoneId: string) {
    reviewMutation.reset();
    setSelectedMilestoneId(milestoneId);
    setReviewingMilestoneId(milestoneId);
  }

  function closeReviewDialog() {
    if (reviewMutation.isPending) return;
    setReviewingMilestoneId(null);
  }

  async function refresh() {
    await topicsQuery.refetch();
    if (effectiveGroupId) {
      await Promise.all([syllabusQuery.refetch(), milestonesQuery.refetch()]);
    }
  }

  return {
    batches,
    selectedBatchId,
    setSelectedBatchId: handleBatchChange,
    currentBatch,
    batchesLoading: batchesQuery.isLoading,

    advisedGroups,
    selectedGroup,
    selectedGroupId: effectiveGroupId,
    setSelectedGroupId: handleGroupChange,
    groupsLoading: topicsQuery.isLoading,
    topicsError: topicsQuery.error
      ? getThesisErrorMessage(topicsQuery.error, 'Không tải được danh sách nhóm.')
      : null,

    syllabusStatus,
    syllabusLoading: syllabusQuery.isLoading,

    milestones,
    filteredMilestones,
    stats,
    statusFilter,
    setStatusFilter,

    selectedMilestone,
    selectedMilestoneId: effectiveMilestoneId,
    setSelectedMilestoneId,
    selectMilestone: handleSelectMilestone,

    milestonesLoading: milestonesQuery.isLoading,
    isFetching:
      topicsQuery.isFetching || syllabusQuery.isFetching || milestonesQuery.isFetching,
    milestonesError: milestonesQuery.error
      ? getThesisErrorMessage(milestonesQuery.error, 'Không tải được danh sách mốc tiến độ.')
      : null,

    canCreateMilestones,
    ineligibleReason,

    createDialogOpen,
    openCreateDialog,
    closeCreateDialog,
    createMilestone: createMutation.mutateAsync,
    isCreating: createMutation.isPending,
    createError: createMutation.error
      ? getThesisErrorMessage(createMutation.error, 'Không thể tạo mốc tiến độ.')
      : null,

    reviewingMilestoneId,
    reviewingMilestone,
    openReviewDialog,
    closeReviewDialog,
    reviewReport: (milestoneId: string, data: ProgressReportReviewRequest) =>
      reviewMutation.mutateAsync({ milestoneId, data }),
    isReviewing: reviewMutation.isPending,
    reviewError: reviewMutation.error
      ? getThesisErrorMessage(reviewMutation.error, 'Không thể gửi đánh giá.')
      : null,

    downloadingFileId,
    handleDownloadFile,
    refresh,
  };
}
