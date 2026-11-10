import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { councilApprovalApi } from '../services/councilApprovalApi';
import type {
  CouncilDraftItemDto,
  CouncilApprovalStatus,
} from '../types/councilApproval.types';

function formatLocalDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function useCouncilApproval() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [userBatchFilter, setUserBatchFilter] = useState<string | null>(null);
  const [appliedBatchFilter, setAppliedBatchFilter] = useState<string | null>(null);
  const [keyword, setKeyword] = useState<string>('');
  const [appliedKeyword, setAppliedKeyword] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<CouncilApprovalStatus>('all');
  const [appliedStatusFilter, setAppliedStatusFilter] = useState<CouncilApprovalStatus>('all');

  const [userSelectedCouncilId, setUserSelectedCouncilId] = useState<string | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<Record<string, File | null>>({});
  const [feedbackOverrides, setFeedbackOverrides] = useState<Record<string, string>>({});
  const [decisionNumberOverrides, setDecisionNumberOverrides] = useState<Record<string, string>>({});

  const [noticeMessage, setNoticeMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const batches = useMemo(() => batchesQuery.data || [], [batchesQuery.data]);

  const defaultBatch = useMemo(() => {
    return batches.find((b) => b.status === 'Open') || batches[0] || null;
  }, [batches]);

  const batchFilter = userBatchFilter !== null ? userBatchFilter : defaultBatch?.id || 'all';
  const appliedBatchId = appliedBatchFilter !== null ? appliedBatchFilter : defaultBatch?.id || 'all';

  const selectedBatch = useMemo(
    () => batches.find((b) => b.id === appliedBatchId) || null,
    [batches, appliedBatchId],
  );

  const isBatchReady = !batchesQuery.isFetching || appliedBatchFilter !== null;

  const draftsQuery = useQuery({
    queryKey: ['council-drafts', appliedBatchId, appliedKeyword, appliedStatusFilter],
    queryFn: ({ signal }) =>
      councilApprovalApi.getCouncilDrafts(
        {
          batchId: appliedBatchId,
          keyword: appliedKeyword,
          status: appliedStatusFilter,
        },
        signal,
      ),
    enabled: isBatchReady,
  });

  const filteredDrafts = useMemo(
    () => draftsQuery.data || [],
    [draftsQuery.data],
  );

  const pendingCount = useMemo(
    () => filteredDrafts.filter((d) => d.status === 'Draft').length,
    [filteredDrafts],
  );

  const selectedCouncilId = useMemo(() => {
    if (userSelectedCouncilId && filteredDrafts.some((d) => d.id === userSelectedCouncilId)) {
      return userSelectedCouncilId;
    }
    return filteredDrafts[0]?.id || null;
  }, [userSelectedCouncilId, filteredDrafts]);

  const selectedCouncil: CouncilDraftItemDto | null = useMemo(
    () => filteredDrafts.find((d) => d.id === selectedCouncilId) || null,
    [filteredDrafts, selectedCouncilId],
  );

  const selectedFile = (selectedCouncilId && selectedFiles[selectedCouncilId]) || null;
  const setSelectedFile = (file: File | null) => {
    if (selectedCouncilId) {
      setSelectedFiles((prev) => ({ ...prev, [selectedCouncilId]: file }));
    }
  };

  const customFeedback =
    selectedCouncilId && feedbackOverrides[selectedCouncilId] !== undefined
      ? feedbackOverrides[selectedCouncilId]
      : selectedCouncil?.feedback || '';

  const setCustomFeedback = (val: string) => {
    if (selectedCouncilId) {
      setFeedbackOverrides((prev) => ({ ...prev, [selectedCouncilId]: val }));
    }
  };

  const customDecisionNumber =
    selectedCouncilId && decisionNumberOverrides[selectedCouncilId] !== undefined
      ? decisionNumberOverrides[selectedCouncilId]
      : selectedCouncil?.decisionNumber ||
        (selectedCouncil?.councilCode ? `QD-${selectedCouncil.councilCode}` : '');

  const setCustomDecisionNumber = (val: string) => {
    if (selectedCouncilId) {
      setDecisionNumberOverrides((prev) => ({ ...prev, [selectedCouncilId]: val }));
    }
  };

  const handleSelectCouncil = (id: string) => {
    setUserSelectedCouncilId(id);
    setNoticeMessage(null);
    setErrorMessage(null);
  };

  const handleApplyFilter = () => {
    setAppliedKeyword(keyword.trim());
    setAppliedStatusFilter(statusFilter);
    setAppliedBatchFilter(batchFilter);
  };

  const handleResetFilter = () => {
    setKeyword('');
    setAppliedKeyword('');
    setStatusFilter('all');
    setAppliedStatusFilter('all');
    setUserBatchFilter(null);
    setAppliedBatchFilter(null);
  };

  const canApprove = useMemo(() => {
    const roles = user?.roles || [];
    return roles.some((r) =>
      ['Admin', 'AcademicAffairs'].some((role) => role.toLowerCase() === r.toLowerCase()),
    );
  }, [user?.roles]);

  const approveMutation = useMutation({
    mutationFn: async () => {
      setNoticeMessage(null);
      setErrorMessage(null);

      if (!selectedCouncil) {
        throw new Error('Vui lòng chọn một dự thảo Hội đồng.');
      }

      const decisionNum = customDecisionNumber.trim();
      if (!decisionNum) {
        throw new Error('Vui lòng nhập số quyết định thành lập Hội đồng.');
      }

      const fileUrl = selectedFile?.name || selectedCouncil.decisionFileUrl || null;

      return await councilApprovalApi.approveCouncil(selectedCouncil.id, {
        decisionNumber: decisionNum,
        signedDate: formatLocalDate(new Date()),
        decisionFileUrl: fileUrl,
      });
    },
    onSuccess: () => {
      setNoticeMessage(`Đã phê duyệt quyết định thành lập ${selectedCouncil?.councilCode || 'Hội đồng'} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['council-drafts'] });
    },
    onError: (err: Error) => {
      setErrorMessage(err.message || 'Phê duyệt Hội đồng thất bại. Vui lòng thử lại.');
    },
  });

  const loadErrorMessage = draftsQuery.isError
    ? draftsQuery.error instanceof Error
      ? draftsQuery.error.message
      : 'Không thể tải danh sách Hội đồng.'
    : null;

  return {
    batches,
    batchFilter,
    setBatchFilter: setUserBatchFilter,
    selectedBatch,
    keyword,
    setKeyword,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    draftsQuery,
    filteredDrafts,
    pendingCount,
    selectedCouncilId,
    setSelectedCouncilId: handleSelectCouncil,
    selectedCouncil,
    selectedFile,
    setSelectedFile,
    customFeedback,
    setCustomFeedback,
    customDecisionNumber,
    setCustomDecisionNumber,
    noticeMessage,
    setNoticeMessage,
    errorMessage: errorMessage || loadErrorMessage,
    setErrorMessage,
    canApprove,
    approveMutation,
  };
}