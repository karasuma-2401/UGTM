import { useState, useMemo, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getApiErrorMessage } from '@/shared/api';
import { useAuth } from '@/shared/auth';
import { getActiveOpenBatch } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { notify } from '@/shared/notifications';
import { councilApprovalApi } from '../services/councilApprovalApi';
import {
  evaluateCouncilReadiness,
  isCouncilVisibleForApproval,
  type CouncilReadinessResult,
} from '../utils/councilApprovalReadiness';
import type {
  CouncilDraftItemDto,
  CouncilReadinessFilter,
} from '../types/councilApproval.types';

export type CouncilApprovalSortField = 'councilCode' | 'defenseDate' | 'status';

function formatLocalDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function useCouncilApproval() {
  const queryClient = useQueryClient();
  const { user, hasRole } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [batchFilterOverride, setBatchFilterOverride] = useState<string | null>(null);
  const [keyword, setKeywordState] = useState<string>('');
  const [readinessFilter, setReadinessFilterState] = useState<CouncilReadinessFilter>('all');
  const [sort, setSort] = useState<{
    field: CouncilApprovalSortField;
    direction: 'asc' | 'desc';
  }>({ field: 'status', direction: 'asc' });
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSizeState] = useState<number>(10);

  const [userSelectedCouncilId, setUserSelectedCouncilId] = useState<string | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<Record<string, File | null>>({});
  const [fileUrlOverrides, setFileUrlOverrides] = useState<Record<string, string>>({});
  const [fileError, setFileError] = useState<string | null>(null);
  const [decisionNumberOverrides, setDecisionNumberOverrides] = useState<Record<string, string>>({});
  const [signedDateOverrides, setSignedDateOverrides] = useState<Record<string, string>>({});
  const [confirmOpen, setConfirmOpen] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const batches = useMemo(() => batchesQuery.data || [], [batchesQuery.data]);
  const defaultBatchFilter = useMemo(
    () => getActiveOpenBatch(batches)?.id ?? 'all',
    [batches],
  );
  const batchFilter = batchFilterOverride ?? defaultBatchFilter;

  const selectedBatch = useMemo(
    () => (batchFilter === 'all' ? null : batches.find((b) => b.id === batchFilter) || null),
    [batches, batchFilter],
  );

  const draftsQuery = useQuery({
    queryKey: ['council-drafts', user?.id, batchFilter],
    queryFn: ({ signal }) =>
      councilApprovalApi.getCouncilDrafts({ batchId: batchFilter }, signal),
    enabled: Boolean(user?.id),
  });

  const allDrafts = useMemo(
    () => (draftsQuery.data || []).filter(isCouncilVisibleForApproval),
    [draftsQuery.data],
  );

  const readinessMap = useMemo(() => {
    const map = new Map<string, CouncilReadinessResult>();
    for (const item of allDrafts) {
      map.set(item.id, evaluateCouncilReadiness(item));
    }
    return map;
  }, [allDrafts]);

  const stats = useMemo(() => {
    let ready = 0;
    let needsAction = 0;
    let approved = 0;
    for (const item of allDrafts) {
      const readiness = readinessMap.get(item.id);
      if (readiness?.category === 'approved') approved += 1;
      else if (readiness?.category === 'ready') ready += 1;
      else needsAction += 1;
    }
    return {
      total: allDrafts.length,
      ready,
      needsAction,
      approved,
    };
  }, [allDrafts, readinessMap]);

  const filteredDrafts = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();

    const filtered = allDrafts.filter((item) => {
      if (readinessFilter !== 'all') {
        const readiness = readinessMap.get(item.id);
        if (readiness?.category !== readinessFilter) {
          return false;
        }
      }

      if (!normalizedKeyword) return true;

      const haystack = [
        item.councilCode,
        item.facultyName,
        item.room,
        item.timeSlot,
        item.groupCode,
        item.decisionNumber ?? '',
        item.chairmanName ?? '',
        item.secretaryName ?? '',
        ...(item.memberNames ?? []),
        ...item.topicTitles,
      ]
        .join(' ')
        .toLowerCase();

      return haystack.includes(normalizedKeyword);
    });

    const direction = sort.direction === 'asc' ? 1 : -1;
    return [...filtered].sort((a, b) => {
      if (sort.field === 'councilCode') {
        return a.councilCode.localeCompare(b.councilCode, 'vi') * direction;
      }
      if (sort.field === 'status') {
        const rank = (item: CouncilDraftItemDto) => {
          const cat = readinessMap.get(item.id)?.category;
          if (cat === 'ready') return 0;
          if (cat === 'needs_action') return 1;
          if (item.status === 'Active') return 2;
          return 3;
        };
        const diff = (rank(a) - rank(b)) * direction;
        if (diff !== 0) return diff;
        const dateDiff = a.defenseDate.localeCompare(b.defenseDate);
        return dateDiff !== 0 ? dateDiff : a.councilCode.localeCompare(b.councilCode, 'vi');
      }
      const dateDiff = a.defenseDate.localeCompare(b.defenseDate) * direction;
      return dateDiff !== 0 ? dateDiff : a.councilCode.localeCompare(b.councilCode, 'vi');
    });
  }, [allDrafts, readinessFilter, readinessMap, keyword, sort]);

  const totalPages = Math.max(1, Math.ceil(filteredDrafts.length / pageSize));
  const safePageNumber = Math.min(pageNumber, totalPages);

  const pagedDrafts = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredDrafts.slice(start, start + pageSize);
  }, [filteredDrafts, safePageNumber, pageSize]);

  const selectedCouncilId = useMemo(() => {
    if (userSelectedCouncilId && filteredDrafts.some((d) => d.id === userSelectedCouncilId)) {
      return userSelectedCouncilId;
    }
    return pagedDrafts[0]?.id || filteredDrafts[0]?.id || null;
  }, [userSelectedCouncilId, filteredDrafts, pagedDrafts]);

  const selectedCouncil: CouncilDraftItemDto | null = useMemo(
    () => filteredDrafts.find((d) => d.id === selectedCouncilId) || null,
    [filteredDrafts, selectedCouncilId],
  );

  const selectedReadiness: CouncilReadinessResult | null = useMemo(
    () => (selectedCouncil ? readinessMap.get(selectedCouncil.id) ?? null : null),
    [selectedCouncil, readinessMap],
  );

  const scheduleQuery = useQuery({
    queryKey: ['council-schedule', user?.id, selectedCouncilId],
    queryFn: ({ signal }) => councilApprovalApi.getCouncilSchedule(selectedCouncilId!, signal),
    enabled: Boolean(user?.id && selectedCouncilId),
    staleTime: 30_000,
  });

  const selectedFile = (selectedCouncilId && selectedFiles[selectedCouncilId]) || null;

  const setSelectedFile = useCallback(
    (file: File | null) => {
      if (!selectedCouncilId) return;
      if (file) {
        if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
          setFileError('Vui lòng chỉ chọn tệp định dạng PDF.');
          return;
        }
        if (file.size > 20 * 1024 * 1024) {
          setFileError('Kích thước tệp vượt quá giới hạn 20 MB.');
          return;
        }
      }
      setFileError(null);
      setSelectedFiles((prev) => ({ ...prev, [selectedCouncilId]: file }));
      if (file) {
        setFileUrlOverrides((prev) => ({ ...prev, [selectedCouncilId]: file.name }));
      } else {
        setFileUrlOverrides((prev) => ({ ...prev, [selectedCouncilId]: '' }));
      }
    },
    [selectedCouncilId],
  );

  const customDecisionFileUrl =
    selectedCouncilId && fileUrlOverrides[selectedCouncilId] !== undefined
      ? fileUrlOverrides[selectedCouncilId]
      : selectedCouncil?.decisionFileUrl || '';

  const setCustomDecisionFileUrl = useCallback(
    (val: string) => {
      if (!selectedCouncilId) return;
      setFileError(null);
      setFileUrlOverrides((prev) => ({ ...prev, [selectedCouncilId]: val }));
      if (!val.trim()) {
        setSelectedFiles((prev) => ({ ...prev, [selectedCouncilId]: null }));
      }
    },
    [selectedCouncilId],
  );

  const customDecisionNumber =
    selectedCouncilId && decisionNumberOverrides[selectedCouncilId] !== undefined
      ? decisionNumberOverrides[selectedCouncilId]
      : selectedCouncil?.decisionNumber ||
        (selectedCouncil?.councilCode ? `QĐ-${selectedCouncil.councilCode}` : '');

  const setCustomDecisionNumber = useCallback(
    (val: string) => {
      if (selectedCouncilId) {
        setDecisionNumberOverrides((prev) => ({ ...prev, [selectedCouncilId]: val }));
      }
    },
    [selectedCouncilId],
  );

  const defaultToday = useMemo(() => formatLocalDate(new Date()), []);

  const customSignedDate =
    selectedCouncilId && signedDateOverrides[selectedCouncilId] !== undefined
      ? signedDateOverrides[selectedCouncilId]
      : selectedCouncil?.signedDate || defaultToday;

  const setCustomSignedDate = useCallback(
    (val: string) => {
      if (selectedCouncilId) {
        setSignedDateOverrides((prev) => ({ ...prev, [selectedCouncilId]: val }));
      }
    },
    [selectedCouncilId],
  );

  const setBatchFilter = useCallback((next: string) => {
    setBatchFilterOverride(next);
    setPageNumber(1);
    setErrorMessage(null);
  }, []);

  const setKeyword = useCallback((next: string) => {
    setKeywordState(next);
    setPageNumber(1);
  }, []);

  const setReadinessFilter = useCallback((next: CouncilReadinessFilter) => {
    setReadinessFilterState(next);
    setPageNumber(1);
  }, []);

  const handleSortChange = useCallback((field: CouncilApprovalSortField) => {
    setSort((prev) => ({
      field,
      direction: prev.field === field && prev.direction === 'asc' ? 'desc' : 'asc',
    }));
    setPageNumber(1);
  }, []);

  const setPageSize = useCallback((size: number) => {
    setPageSizeState(size);
    setPageNumber(1);
  }, []);

  const handleSelectCouncil = useCallback((id: string) => {
    setUserSelectedCouncilId(id);
    setFileError(null);
    setErrorMessage(null);
  }, []);

  const handleResetFilter = useCallback(() => {
    setKeywordState('');
    setReadinessFilterState('all');
    setBatchFilterOverride(null);
    setPageNumber(1);
    setErrorMessage(null);
  }, []);

  const canApprove = hasRole(['Admin', 'AcademicAffairs']);

  const approveMutation = useMutation({
    mutationFn: async () => {
      setErrorMessage(null);

      if (!selectedCouncil) {
        throw new Error('Vui lòng chọn một dự thảo Hội đồng.');
      }

      const decisionNum = customDecisionNumber.trim();
      if (!decisionNum) {
        throw new Error('Vui lòng nhập số quyết định thành lập Hội đồng.');
      }
      if (decisionNum.length > 100) {
        throw new Error('Số quyết định không được vượt quá 100 ký tự.');
      }

      const trimmedFileUrl = (customDecisionFileUrl || selectedFile?.name || '').trim();
      if (trimmedFileUrl.length > 500) {
        throw new Error('Đường dẫn hoặc tên tệp quyết định không được vượt quá 500 ký tự.');
      }

      return await councilApprovalApi.approveCouncil(selectedCouncil.id, {
        decisionNumber: decisionNum,
        signedDate: customSignedDate || defaultToday,
        decisionFileUrl: trimmedFileUrl || null,
      });
    },
    onSuccess: (res) => {
      setConfirmOpen(false);
      notify.success(`Đã phê duyệt quyết định ${res.decisionNumber || res.councilCode} thành công.`);
      void queryClient.invalidateQueries({ queryKey: ['council-drafts'] });
      void queryClient.invalidateQueries({ queryKey: ['council-schedule', user?.id, res.id] });
    },
    onError: (err: unknown) => {
      setConfirmOpen(false);
      const message = getApiErrorMessage(err, 'Phê duyệt Hội đồng thất bại. Vui lòng thử lại.');
      setErrorMessage(message);
      notify.error(message);
    },
  });

  const handleRequestApprove = useCallback(() => {
    setErrorMessage(null);
    if (!selectedCouncil) return;
    if (!customDecisionNumber.trim()) {
      setErrorMessage('Vui lòng nhập số quyết định thành lập Hội đồng.');
      return;
    }
    if (fileError) return;
    setConfirmOpen(true);
  }, [selectedCouncil, customDecisionNumber, fileError]);

  const loadErrorMessage = draftsQuery.isError
    ? getApiErrorMessage(draftsQuery.error, 'Không thể tải danh sách Hội đồng.')
    : null;

  return {
    batches,
    isLoadingBatches: batchesQuery.isLoading,
    batchFilter,
    setBatchFilter,
    selectedBatch,
    keyword,
    setKeyword,
    readinessFilter,
    setReadinessFilter,
    sort,
    handleSortChange,
    pageNumber: safePageNumber,
    setPageNumber,
    pageSize,
    setPageSize,
    handleResetFilter,
    draftsQuery,
    allDraftsCount: allDrafts.length,
    filteredDrafts,
    pagedDrafts,
    readinessMap,
    stats,
    selectedCouncilId,
    setSelectedCouncilId: handleSelectCouncil,
    selectedCouncil,
    selectedReadiness,
    scheduleQuery,
    selectedFile,
    setSelectedFile,
    customDecisionFileUrl,
    setCustomDecisionFileUrl,
    fileError,
    customDecisionNumber,
    setCustomDecisionNumber,
    customSignedDate,
    setCustomSignedDate,
    confirmOpen,
    setConfirmOpen,
    handleRequestApprove,
    errorMessage: errorMessage || loadErrorMessage,
    setErrorMessage,
    canApprove,
    approveMutation,
  };
}
