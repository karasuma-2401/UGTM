import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '@/features/thesis';
import { reviewerAssignmentApi } from '../services/reviewerAssignmentApi';
import type {
  ReviewerAssignmentEligibilityItemDto,
  ReviewerAssignmentFilterStatus,
} from '../types/reviewerAssignment.types';

export function useReviewerAssignment() {
  const queryClient = useQueryClient();
  const { user, hasPermission } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [userSelectedBatchId, setUserSelectedBatchId] = useState<string>('');
  const [keyword, setKeyword] = useState<string>('');
  const [appliedKeyword, setAppliedKeyword] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<ReviewerAssignmentFilterStatus>('all');
  const [appliedStatusFilter, setAppliedStatusFilter] = useState<ReviewerAssignmentFilterStatus>('all');

  const [userSelectedGroupId, setUserSelectedGroupId] = useState<string | null>(null);
  const [selectedReviewerId, setSelectedReviewerId] = useState<string>('');

  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const batches = useMemo(() => batchesQuery.data || [], [batchesQuery.data]);

  const defaultBatch = useMemo(() => {
    return batches.find((b) => b.status === 'Open') || batches[0] || null;
  }, [batches]);

  const selectedBatchId = userSelectedBatchId || defaultBatch?.id || '';

  const selectedBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || defaultBatch,
    [batches, selectedBatchId, defaultBatch],
  );

  const eligibilityQuery = useQuery({
    queryKey: ['reviewer-eligibility', selectedBatch?.id, appliedKeyword],
    queryFn: ({ signal }) =>
      reviewerAssignmentApi.getEligibilityList(
        {
          batchId: selectedBatch!.id,
          includeAssigned: true,
          keyword: appliedKeyword,
          pageSize: 100,
        },
        signal,
      ),
    enabled: Boolean(selectedBatch?.id),
  });

  const rawItems = useMemo(
    () => eligibilityQuery.data?.items || [],
    [eligibilityQuery.data?.items],
  );

  const filteredItems = useMemo(() => {
    if (appliedStatusFilter === 'pending') {
      return rawItems.filter((item) => !item.hasActiveAssignment);
    }
    if (appliedStatusFilter === 'assigned') {
      return rawItems.filter((item) => item.hasActiveAssignment);
    }
    return rawItems;
  }, [rawItems, appliedStatusFilter]);

  const pendingCount = useMemo(
    () => rawItems.filter((item) => !item.hasActiveAssignment).length,
    [rawItems],
  );

  const selectedGroupId = useMemo(() => {
    if (userSelectedGroupId && filteredItems.some((i) => i.groupId === userSelectedGroupId)) {
      return userSelectedGroupId;
    }
    return filteredItems[0]?.groupId || null;
  }, [userSelectedGroupId, filteredItems]);

  const selectedItem: ReviewerAssignmentEligibilityItemDto | null = useMemo(
    () => filteredItems.find((i) => i.groupId === selectedGroupId) || null,
    [filteredItems, selectedGroupId],
  );

  const handleSelectGroup = (groupId: string | null) => {
    setUserSelectedGroupId(groupId);
    setSelectedReviewerId('');
    setNotice(null);
    setErrorMessage(null);
  };

  const groupDetailQuery = useQuery({
    queryKey: ['student-group-detail', selectedGroupId],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(
        selectedGroupId!,
        {
          topicId: selectedItem?.topicId,
          topicTitle: selectedItem?.topicTitleVi,
          batchName: selectedBatch?.batchName,
        },
        signal,
      ),
    enabled: Boolean(selectedGroupId),
  });

  const candidatesQuery = useQuery({
    queryKey: ['reviewer-candidates', selectedGroupId],
    queryFn: ({ signal }) => reviewerAssignmentApi.getCandidates(selectedGroupId!, undefined, signal),
    enabled: Boolean(selectedGroupId),
  });

  const candidates = useMemo(
    () => candidatesQuery.data?.items || [],
    [candidatesQuery.data?.items],
  );

  const handleApplyFilter = () => {
    setAppliedKeyword(keyword.trim());
    setAppliedStatusFilter(statusFilter);
  };

  const handleResetFilter = () => {
    setKeyword('');
    setAppliedKeyword('');
    setStatusFilter('all');
    setAppliedStatusFilter('all');
  };

  const canManage = useMemo(() => {
    const roles = user?.roles || [];
    const isPrivileged = roles.some((r) =>
      ['Admin', 'Faculty', 'AcademicAffairs'].some((role) => role.toLowerCase() === r.toLowerCase()),
    );
    return isPrivileged || hasPermission('thesis.reviewer-assignments.manage');
  }, [user?.roles, hasPermission]);

  const assignMutation = useMutation({
    mutationFn: async () => {
      setNotice(null);
      setErrorMessage(null);

      if (!selectedItem) {
        throw new Error('Vui lòng chọn hồ sơ cần phân công.');
      }
      if (!selectedReviewerId) {
        throw new Error('Vui lòng chọn một Giảng viên phản biện.');
      }

      return await reviewerAssignmentApi.createAssignment({
        groupId: selectedItem.groupId,
        reviewerId: selectedReviewerId,
        expectedAdvisorEvaluationId: selectedItem.advisorEvaluationId,
        expectedReportVersionNo: selectedItem.reportVersionNo,
      });
    },
    onSuccess: () => {
      setNotice('Phân công Giảng viên phản biện thành công!');
      setSelectedReviewerId('');
      void queryClient.invalidateQueries({ queryKey: ['reviewer-eligibility'] });
    },
    onError: (err: Error) => {
      setErrorMessage(err.message || 'Phân công phản biện thất bại. Vui lòng thử lại.');
    },
  });

  return {
    batches,
    selectedBatch,
    selectedBatchId,
    setSelectedBatchId: setUserSelectedBatchId,
    keyword,
    setKeyword,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    eligibilityQuery,
    filteredItems,
    pendingCount,
    selectedGroupId,
    setSelectedGroupId: handleSelectGroup,
    selectedItem,
    groupDetailQuery,
    candidatesQuery,
    candidates,
    selectedReviewerId,
    setSelectedReviewerId,
    notice,
    setNotice,
    errorMessage,
    setErrorMessage,
    canManage,
    assignMutation,
  };
}