import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { councilApprovalApi } from '../services/councilApprovalApi';
import type {
  CouncilDraftItemDto,
  CouncilApprovalStatus,
} from '../types/councilApproval.types';

export function useCouncilApproval() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [userSelectedBatchId, setUserSelectedBatchId] = useState<string>('');
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

  const selectedBatchId = userSelectedBatchId || defaultBatch?.id || '';

  const selectedBatch = useMemo(
    () => batches.find((b) => b.id === selectedBatchId) || defaultBatch,
    [batches, selectedBatchId, defaultBatch],
  );

  const draftsQuery = useQuery({
    queryKey: ['council-drafts', selectedBatch?.id, appliedKeyword, appliedStatusFilter],
    queryFn: ({ signal }) =>
      councilApprovalApi.getCouncilDrafts(
        {
          batchId: selectedBatch?.id,
          keyword: appliedKeyword,
          status: appliedStatusFilter,
        },
        signal,
      ),
  });

  const rawDrafts = useMemo(
    () => draftsQuery.data || [],
    [draftsQuery.data],
  );

  const filteredDrafts = useMemo(() => {
    return rawDrafts.filter((item) => {
      const matchKeyword =
        !appliedKeyword.trim() ||
        item.councilCode.toLowerCase().includes(appliedKeyword.toLowerCase()) ||
        item.facultyName.toLowerCase().includes(appliedKeyword.toLowerCase()) ||
        item.groupCode.toLowerCase().includes(appliedKeyword.toLowerCase());

      const matchStatus =
        appliedStatusFilter === 'all' ||
        (appliedStatusFilter === 'draft' && item.status === 'Draft') ||
        (appliedStatusFilter === 'active' && item.status === 'Active');

      return matchKeyword && matchStatus;
    });
  }, [rawDrafts, appliedKeyword, appliedStatusFilter]);

  const pendingCount = useMemo(
    () => rawDrafts.filter((d) => d.status === 'Draft').length,
    [rawDrafts],
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
  };

  const handleResetFilter = () => {
    setKeyword('');
    setAppliedKeyword('');
    setStatusFilter('all');
    setAppliedStatusFilter('all');
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

      const decisionNum =
        customDecisionNumber.trim() || `QD-${selectedCouncil.councilCode}`;
      const fileName = selectedFile?.name || selectedCouncil.decisionFileUrl || `${decisionNum}.pdf`;

      return await councilApprovalApi.approveCouncil(selectedCouncil.id, {
        decisionNumber: decisionNum,
        signedDate: new Date().toISOString().split('T')[0],
        decisionFileUrl: fileName,
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
    errorMessage,
    setErrorMessage,
    canApprove,
    approveMutation,
  };
}
