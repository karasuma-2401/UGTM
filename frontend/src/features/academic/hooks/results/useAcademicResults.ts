import { useCallback, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { notify } from '@/shared/notifications';
import { academicResultsApi, extractApiError } from '../../services/academicResultsApi';
import type {
  MergedResultRow,
  ResultFilterKey,
  ResultsExportFormat,
  ResultSortField,
} from '../../types/results.types';
import {
  extractPublicationFailureMessage,
  mapFilterKeyToExportStatus,
  mergeScoreAndAcademicRows,
} from '../../utils/scoreSummaryFormatting';

export function useAcademicResults() {
  const queryClient = useQueryClient();
  const { user, hasRole, hasPermission } = useAuth();

  const canRead =
    hasRole('Admin') ||
    ((hasRole('Faculty') || hasRole('AcademicAffairs')) &&
      hasPermission('councils.score-summaries.read'));
  const canCalculate =
    hasRole('Admin') || (hasRole('Faculty') && hasPermission('councils.score-summaries.manage'));
  const canPublish =
    hasRole('Admin') || (hasRole('Faculty') && hasPermission('councils.score-summaries.publish'));
  const canExport = hasRole(['Admin', 'Faculty', 'AcademicAffairs']);

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [selectedBatchId, setSelectedBatchId] = useState<string>('');
  const [keyword, setKeyword] = useState('');
  const [filterKey, setFilterKey] = useState<ResultFilterKey>('all');
  const [sort, setSort] = useState<{ field: ResultSortField; direction: 'asc' | 'desc' }>({
    field: 'groupCode',
    direction: 'asc',
  });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [inspectedGroupId, setInspectedGroupId] = useState<string | null>(null);
  const [checkedGroupIds, setCheckedGroupIds] = useState<string[]>([]);
  const [draftDialogGroupId, setDraftDialogGroupId] = useState<string | null>(null);
  const [publishConfirmGroupIds, setPublishConfirmGroupIds] = useState<string[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const resolvedBatchId = useMemo(() => {
    if (selectedBatchId && batches.some((batch) => batch.id === selectedBatchId)) {
      return selectedBatchId;
    }
    const current = getCurrentBatches(batches)[0];
    const open = batches.find((batch) => batch.status === 'Open');
    return (current ?? open ?? batches[0])?.id ?? '';
  }, [batches, selectedBatchId]);

  const selectedBatch = useMemo(
    () => batches.find((batch) => batch.id === resolvedBatchId) ?? null,
    [batches, resolvedBatchId],
  );

  const listQuery = useQuery({
    queryKey: ['academic-results-list', user?.id, resolvedBatchId],
    queryFn: async ({ signal }) => {
      const [scoreItems, academicItems] = await Promise.all([
        academicResultsApi.getAllScoreSummaries(resolvedBatchId, signal),
        academicResultsApi.getAllAcademicResults(resolvedBatchId, signal),
      ]);
      return mergeScoreAndAcademicRows(scoreItems, academicItems);
    },
    enabled: Boolean(user?.id && resolvedBatchId && canRead),
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });

  const allRows = useMemo(() => listQuery.data ?? [], [listQuery.data]);
  const rowsByGroupId = useMemo(
    () => new Map(allRows.map((row) => [row.groupId, row])),
    [allRows],
  );

  const stats = useMemo(() => {
    let readyToPublish = 0;
    let needsCalculation = 0;
    let needsAction = 0;
    let published = 0;

    for (const row of allRows) {
      if (row.filterKey === 'published') published += 1;
      else if (row.filterKey === 'readyToPublish') readyToPublish += 1;
      else if (row.filterKey === 'needsCalculation') needsCalculation += 1;
      else needsAction += 1;
    }

    return {
      all: allRows.length,
      readyToPublish,
      needsCalculation,
      needsAction,
      published,
    };
  }, [allRows]);

  const eligibleGroupIds = useMemo(
    () =>
      allRows
        .filter((row) => row.isReadyForPublication && row.summaryVersion !== null)
        .map((row) => row.groupId),
    [allRows],
  );

  const filteredAndSortedRows = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();

    const filtered = allRows.filter((row) => {
      if (filterKey !== 'all' && row.filterKey !== filterKey) {
        return false;
      }
      if (!normalizedKeyword) {
        return true;
      }
      return (
        row.groupCode.toLowerCase().includes(normalizedKeyword) ||
        row.topicTitleVi.toLowerCase().includes(normalizedKeyword) ||
        (row.topicTitleEn?.toLowerCase().includes(normalizedKeyword) ?? false) ||
        row.studentNames.some((name) => name.toLowerCase().includes(normalizedKeyword)) ||
        row.studentCodes.some((code) => code.toLowerCase().includes(normalizedKeyword))
      );
    });

    const factor = sort.direction === 'asc' ? 1 : -1;
    return [...filtered].sort((a, b) => {
      if (sort.field === 'finalScore') {
        const aScore = a.finalScore ?? -1;
        const bScore = b.finalScore ?? -1;
        return (aScore - bScore) * factor || a.groupCode.localeCompare(b.groupCode, 'vi');
      }
      if (sort.field === 'topicTitleVi') {
        return a.topicTitleVi.localeCompare(b.topicTitleVi, 'vi') * factor;
      }
      if (sort.field === 'status') {
        return a.displayStatusLabel.localeCompare(b.displayStatusLabel, 'vi') * factor;
      }
      return a.groupCode.localeCompare(b.groupCode, 'vi') * factor;
    });
  }, [allRows, filterKey, keyword, sort]);

  const totalCount = filteredAndSortedRows.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePageNumber = Math.min(pageNumber, totalPages);

  const pagedRows = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredAndSortedRows.slice(start, start + pageSize);
  }, [filteredAndSortedRows, pageSize, safePageNumber]);

  const resolvedInspectedGroupId = useMemo(() => {
    if (allRows.length === 0) {
      return null;
    }
    if (inspectedGroupId && rowsByGroupId.has(inspectedGroupId)) {
      return inspectedGroupId;
    }
    return (pagedRows[0] ?? allRows[0])?.groupId ?? null;
  }, [allRows, inspectedGroupId, pagedRows, rowsByGroupId]);

  const resolvedCheckedGroupIds = useMemo(
    () =>
      checkedGroupIds.filter((id) => {
        const row = rowsByGroupId.get(id);
        return Boolean(row && row.isReadyForPublication && row.summaryVersion !== null);
      }),
    [checkedGroupIds, rowsByGroupId],
  );

  const inspectedRow: MergedResultRow | null = resolvedInspectedGroupId
    ? (rowsByGroupId.get(resolvedInspectedGroupId) ?? null)
    : null;

  const detailTargetGroupId = draftDialogGroupId ?? resolvedInspectedGroupId;
  const detailTargetRow = detailTargetGroupId ? (rowsByGroupId.get(detailTargetGroupId) ?? null) : null;
  const hasSummaryRecord = Boolean(detailTargetRow?.summaryId);

  const detailQuery = useQuery({
    queryKey: ['academic-result-detail', user?.id, detailTargetGroupId, hasSummaryRecord],
    queryFn: async ({ signal }) => {
      const groupId = detailTargetGroupId!;
      const [summary, readiness, academicDetail] = await Promise.all([
        hasSummaryRecord
          ? academicResultsApi.getScoreSummaryDetail(groupId, signal)
          : Promise.resolve(null),
        academicResultsApi.getScoreReadiness(groupId, signal),
        academicResultsApi.getAcademicResultDetail(groupId, signal),
      ]);
      return { summary, readiness, academicDetail };
    },
    enabled: Boolean(user?.id && detailTargetGroupId && canRead),
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });

  const invalidateResults = useCallback(
    async (groupId?: string) => {
      await queryClient.invalidateQueries({ queryKey: ['academic-results-list'] });
      await (groupId
        ? queryClient.invalidateQueries({
            queryKey: ['academic-result-detail', user?.id, groupId],
          })
        : queryClient.invalidateQueries({ queryKey: ['academic-result-detail'] }));
    },
    [queryClient, user?.id],
  );

  const calculateMutation = useMutation({
    mutationFn: ({
      groupId,
      expectedSummaryVersion,
    }: {
      groupId: string;
      expectedSummaryVersion: number | null;
    }) => academicResultsApi.calculateScore(groupId, { expectedSummaryVersion }),
    onSuccess: async (dto, variables) => {
      setErrorMessage(null);
      await invalidateResults(variables.groupId);
      const code = rowsByGroupId.get(variables.groupId)?.groupCode ?? '';
      notify.success(
        `Đã tính điểm dự thảo${code ? ` nhóm ${code}` : ''}: ${dto.finalScore?.toFixed(2) ?? '—'} điểm.`,
      );
    },
    onError: (error) => {
      const apiError = extractApiError(error);
      setErrorMessage(apiError.message);
      notify.error(apiError.message);
    },
  });

  const publishMutation = useMutation({
    mutationFn: async (groupIds: string[]) => {
      const selections = groupIds
        .map((groupId) => {
          const row = rowsByGroupId.get(groupId);
          if (!row || row.summaryVersion === null) {
            return null;
          }
          return { groupId, expectedSummaryVersion: row.summaryVersion };
        })
        .filter((item): item is { groupId: string; expectedSummaryVersion: number } => item !== null);

      if (selections.length !== groupIds.length || selections.length === 0) {
        throw new Error('Một số nhóm chưa có bảng điểm dự thảo hợp lệ để công bố.');
      }

      return academicResultsApi.publishScores({
        batchId: resolvedBatchId,
        selections,
      });
    },
    onSuccess: async (dto) => {
      setErrorMessage(null);
      setPublishConfirmGroupIds([]);
      setDraftDialogGroupId(null);
      setCheckedGroupIds([]);
      await invalidateResults();
      notify.success(`Đã công bố chính thức kết quả cho ${dto.publishedCount} nhóm.`);
    },
    onError: async (error) => {
      setPublishConfirmGroupIds([]);
      const apiError = extractApiError(error);
      const formatted = extractPublicationFailureMessage(apiError, rowsByGroupId);
      setErrorMessage(formatted);
      notify.error(formatted);
      await invalidateResults();
    },
  });

  const exportMutation = useMutation({
    mutationFn: (format: ResultsExportFormat) =>
      academicResultsApi.exportResultsFile({
        batchId: resolvedBatchId,
        format,
        keyword,
        status: mapFilterKeyToExportStatus(filterKey),
      }),
    onSuccess: (filename) => {
      notify.success(`Đã tải xuống báo cáo ${filename}.`);
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Không thể xuất báo cáo.';
      setErrorMessage(message);
      notify.error(message);
    },
  });

  const handleBatchChange = useCallback((nextBatchId: string) => {
    setSelectedBatchId(nextBatchId);
    setPageNumber(1);
    setCheckedGroupIds([]);
    setInspectedGroupId(null);
    setDraftDialogGroupId(null);
    setPublishConfirmGroupIds([]);
    setErrorMessage(null);
  }, []);

  const handleFilterChange = useCallback((nextFilter: ResultFilterKey) => {
    setFilterKey(nextFilter);
    setPageNumber(1);
  }, []);

  const handleKeywordChange = useCallback((value: string) => {
    setKeyword(value);
    setPageNumber(1);
  }, []);

  const handleSortChange = useCallback((field: ResultSortField) => {
    setSort((prev) =>
      prev.field === field
        ? { field, direction: prev.direction === 'asc' ? 'desc' : 'asc' }
        : { field, direction: 'asc' },
    );
  }, []);

  const toggleGroupChecked = useCallback((groupId: string) => {
    setCheckedGroupIds((prev) =>
      prev.includes(groupId) ? prev.filter((id) => id !== groupId) : [...prev, groupId],
    );
  }, []);

  const toggleAllEligibleChecked = useCallback(() => {
    if (eligibleGroupIds.length === 0) {
      return;
    }
    setCheckedGroupIds((prev) => {
      const allSelected = eligibleGroupIds.every((id) => prev.includes(id));
      return allSelected ? [] : [...eligibleGroupIds];
    });
  }, [eligibleGroupIds]);

  const openPublishConfirm = useCallback(
    (targetIds?: string[]) => {
      const ids =
        targetIds ??
        (resolvedCheckedGroupIds.length > 0 ? resolvedCheckedGroupIds : eligibleGroupIds);
      const validIds = ids.filter((id) => {
        const row = rowsByGroupId.get(id);
        return Boolean(row && row.isReadyForPublication && row.summaryVersion !== null);
      });
      if (validIds.length === 0) {
        notify.warning('Chưa có nhóm đủ điều kiện sẵn sàng công bố.');
        return;
      }
      setPublishConfirmGroupIds(validIds);
    },
    [eligibleGroupIds, resolvedCheckedGroupIds, rowsByGroupId],
  );

  const publishConfirmRows = useMemo(
    () =>
      publishConfirmGroupIds
        .map((id) => rowsByGroupId.get(id))
        .filter((row): row is MergedResultRow => Boolean(row)),
    [publishConfirmGroupIds, rowsByGroupId],
  );

  const draftDialogRow = draftDialogGroupId ? (rowsByGroupId.get(draftDialogGroupId) ?? null) : null;

  return {
    permissions: { canRead, canCalculate, canPublish, canExport },
    batches,
    selectedBatch,
    selectedBatchId: resolvedBatchId,
    isLoadingBatches: batchesQuery.isLoading,
    rows: pagedRows,
    allRowsCount: allRows.length,
    totalCount,
    pageNumber: safePageNumber,
    pageSize,
    stats,
    eligibleGroupIds,
    keyword,
    filterKey,
    sort,
    inspectedGroupId: resolvedInspectedGroupId,
    inspectedRow,
    checkedGroupIds: resolvedCheckedGroupIds,
    detailData: detailQuery.data ?? null,
    isLoadingDetail: detailQuery.isLoading,
    detailError: detailQuery.isError ? extractApiError(detailQuery.error).message : null,
    draftDialogGroupId,
    draftDialogRow,
    publishConfirmRows,
    errorMessage,
    isLoadingList: listQuery.isLoading,
    isFetchingList: listQuery.isFetching,
    listError: listQuery.isError ? extractApiError(listQuery.error).message : null,
    isCalculating: calculateMutation.isPending,
    calculatingGroupId: calculateMutation.variables?.groupId ?? null,
    isPublishing: publishMutation.isPending,
    isExporting: exportMutation.isPending,
    exportingFormat: exportMutation.variables ?? null,
    actions: {
      handleBatchChange,
      handleFilterChange,
      handleKeywordChange,
      handleSortChange,
      setPageNumber,
      setPageSize: (nextSize: number) => {
        setPageSize(nextSize);
        setPageNumber(1);
      },
      setInspectedGroupId,
      toggleGroupChecked,
      toggleAllEligibleChecked,
      clearCheckedGroups: () => setCheckedGroupIds([]),
      openDraftDialog: (groupId: string) => setDraftDialogGroupId(groupId),
      closeDraftDialog: () => setDraftDialogGroupId(null),
      openPublishConfirm,
      closePublishConfirm: () => setPublishConfirmGroupIds([]),
      calculateScore: (groupId: string, expectedSummaryVersion: number | null) =>
        calculateMutation.mutate({ groupId, expectedSummaryVersion }),
      confirmPublish: () => {
        if (publishConfirmGroupIds.length > 0) {
          publishMutation.mutate(publishConfirmGroupIds);
        }
      },
      exportReport: (format: ResultsExportFormat) => exportMutation.mutate(format),
      refreshList: () => invalidateResults(resolvedInspectedGroupId ?? undefined),
      dismissError: () => setErrorMessage(null),
    },
  };
}
